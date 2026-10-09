using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Api.Identity;
using ShuttleBook.Api.Bookings;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Onboarding;

// Local private-object adapter for Development. Production intentionally fails closed until S3 is configured.
public static class MediaEndpoints
{
    private sealed record PresignInput(Guid VenueId, string? Purpose, string? ContentType, long SizeBytes,
        string? Sha256Base64);

    public static void MapMediaEndpoints(this WebApplication app)
    {
        var uploads = app.MapGroup("/api/v1/uploads");
        uploads.MapPost("/presign", Presign).RequireAuthorization();
        uploads.MapPut("/{uploadId:guid}/content", PutContent);
        uploads.MapPost("/{uploadId:guid}/complete", Complete).RequireAuthorization();
        uploads.MapGet("/{uploadId:guid}/view", View).RequireAuthorization();
        app.MapPost("/api/v1/bookings/{id:guid}/proof-uploads/presign", ProofPresign).RequireAuthorization().RequireRateLimiting("booking-create");
    }

    private static Task<IResult> Presign(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, CancellationToken ct) => PresignCore(http, db, environment, config, null, ct);

    private static Task<IResult> ProofPresign(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, Guid id, CancellationToken ct) => PresignCore(http, db, environment, config, id, ct);

    private static async Task<IResult> PresignCore(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, Guid? bookingId, CancellationToken ct)
    {
        if (!Local(environment, config) && !S3(config))
            return Error(http, 503, "MEDIA_UNAVAILABLE");
        if (http.Request.ContentLength is > 2048) return Error(http, 400, "VALIDATION_FAILED");
        PresignInput? input;
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[1024];
            int count;
            while ((count = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > 2048) return Error(http, 400, "VALIDATION_FAILED");
                buffer.Write(chunk, 0, count);
            }
            buffer.Position = 0;
            using var document = await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Error(http, 400, "VALIDATION_FAILED");
            var allowed = bookingId is null ? new HashSet<string> { "venueId", "purpose", "contentType", "sizeBytes", "sha256Base64" } :
                new HashSet<string> { "contentType", "sizeBytes", "sha256Base64" };
            var seen = new HashSet<string>();
            foreach (var field in document.RootElement.EnumerateObject())
            {
                if (!allowed.Contains(field.Name)) return Error(http, 400, "UNSUPPORTED_FIELD");
                if (!seen.Add(field.Name)) return Error(http, 400, "VALIDATION_FAILED");
            }
            if (allowed.Any(field => !seen.Contains(field))) return Error(http, 400, "VALIDATION_FAILED");
            input = document.RootElement.Deserialize<PresignInput>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { PropertyNameCaseInsensitive = false });
        }
        catch (JsonException) { return Error(http, 400, "VALIDATION_FAILED"); }
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        if (input is null ||
            input.ContentType is not ("image/png" or "image/jpeg" or "image/webp") ||
            input.SizeBytes is < 1 or > 5_242_880 || !ValidSha(input.Sha256Base64))
            return Error(http, 400, "VALIDATION_FAILED");
        var user = await CurrentUser(http, db, ct);
        Guid venueId;
        if (bookingId is Guid proofBookingId)
        {
            if (user?.AccountType != AccountType.Customer || user.Status != UserStatus.Active) return Error(http, 403, "FORBIDDEN");
            var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proofBookingId && x.CustomerId == user.Id, ct);
            if (booking is null) return Error(http, 404, "NOT_FOUND");
            bookingId = await BookingGroup.AnchorId(db, proofBookingId, ct) ?? throw new InvalidOperationException("Missing booking payment scope.");
            if (booking.Status is not ("AWAITING_TRANSFER" or "NEEDS_REVIEW")) return Error(http, 409, "STATE_CONFLICT");
            if (booking.Status == "AWAITING_TRANSFER" && booking.PaymentDeadline <= http.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow()) return Error(http, 409, "PAYMENT_DEADLINE_EXPIRED");
            venueId = booking.VenueId;
            input = input with { VenueId = venueId, Purpose = "PAYMENT_PROOF" };
        }
        else
        {
            if (input.VenueId == Guid.Empty || input.Purpose is not ("QR" or "VENUE_IMAGE")) return Error(http, 400, "VALIDATION_FAILED");
            if (user?.AccountType != AccountType.VenueOperator || user.Status is not (UserStatus.PendingOnboarding or UserStatus.Active)) return Error(http, 403, "FORBIDDEN");
            var venue = await db.Venues.SingleOrDefaultAsync(v => v.Id == input.VenueId, ct);
            if (venue is null || !await db.BusinessMemberships.AnyAsync(m => m.BusinessId == venue.BusinessId &&
                m.UserId == user.Id && m.Role == "OWNER" && (m.Status == "PENDING" || m.Status == "ACTIVE"), ct)) return Error(http, 404, "NOT_FOUND");
            if (venue.Status != "DRAFT" && !(venue.Status == "PUBLISHED" && user.Status == UserStatus.Active && input.Purpose == "QR")) return Error(http, 409, "STATE_CONFLICT");
            venueId = venue.Id;
        }
        var upload = new MediaUpload { OwnerUserId = user!.Id, VenueId = venueId, BookingId = bookingId, Purpose = input.Purpose!,
            ContentType = input.ContentType, SizeBytes = input.SizeBytes, Sha256Base64 = input.Sha256Base64!,
            ObjectKey = bookingId is Guid proofId ? $"payment-proofs/{proofId:N}/{Guid.CreateVersion7():N}" : $"venues/{venueId:N}/{Guid.CreateVersion7():N}", CreatedAt = DateTimeOffset.UtcNow };
        var expiration = DateTimeOffset.UtcNow.AddMinutes(5);
        string url;
        object uploadHeaders;
        if (S3(config))
        {
            var client = http.RequestServices.GetRequiredService<IAmazonS3>();
            var signed = new GetPreSignedUrlRequest { BucketName = config["Media:S3Bucket"], Key = upload.ObjectKey,
                Verb = HttpVerb.PUT, Expires = expiration.UtcDateTime, ContentType = upload.ContentType };
            signed.Headers["x-amz-checksum-sha256"] = upload.Sha256Base64;
            signed.Headers["If-None-Match"] = "*";
            signed.Headers["Content-Length"] = upload.SizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try { url = await client.GetPreSignedURLAsync(signed); }
            catch (AmazonClientException) { return Error(http, 503, "MEDIA_UNAVAILABLE"); }
            uploadHeaders = new Dictionary<string, string> { ["Content-Type"] = upload.ContentType,
                ["x-amz-checksum-sha256"] = upload.Sha256Base64, ["If-None-Match"] = "*" };
        }
        else
        {
            var expires = expiration.ToUnixTimeSeconds();
            var signature = Sign(config, upload.Id, expires, upload.Sha256Base64);
            url = $"{http.Request.Scheme}://{http.Request.Host}/api/v1/uploads/{upload.Id}/content?expires={expires}&sig={Uri.EscapeDataString(signature)}";
            uploadHeaders = new Dictionary<string, string> { ["Content-Type"] = upload.ContentType };
        }
        db.MediaUploads.Add(upload);
        await db.SaveChangesAsync(ct);
        if (bookingId is not null)
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { data = new { upload.Id, upload.ContentType, upload.SizeBytes,
                uploadUrl = url, uploadHeaders, expiresAt = expiration }, traceId = http.TraceIdentifier });
        }
        return Results.Ok(new { data = new { upload.Id, upload.ObjectKey, upload.ContentType, upload.SizeBytes,
            uploadUrl = url, uploadHeaders, expiresAt = expiration }, traceId = http.TraceIdentifier });
    }

    private static async Task<IResult> PutContent(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, Guid uploadId, CancellationToken ct)
    {
        if (!Local(environment, config))
            return Error(http, 503, "MEDIA_UNAVAILABLE");
        if (!long.TryParse(http.Request.Query["expires"], out var expires) ||
            expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds() || expires > DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds())
            return Error(http, 403, "FORBIDDEN");
        var upload = await db.MediaUploads.SingleOrDefaultAsync(u => u.Id == uploadId, ct);
        if (upload is null || upload.Status != "PENDING") return Error(http, 404, "NOT_FOUND");
        var expected = Sign(config, upload.Id, expires, upload.Sha256Base64);
        var supplied = http.Request.Query["sig"].ToString();
        if (supplied.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(expected)))
            return Error(http, 403, "FORBIDDEN");
        if (http.Request.ContentLength != upload.SizeBytes || http.Request.ContentType != upload.ContentType)
            return Error(http, 400, "VALIDATION_FAILED");
        var directory = Path.Combine(environment.ContentRootPath, ".media-local");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, upload.Id.ToString("N"));
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await http.Request.Body.CopyToAsync(file, ct);
            if (file.Length != upload.SizeBytes) { file.Close(); File.Delete(path); return Error(http, 400, "VALIDATION_FAILED"); }
        }
        catch (IOException) { return Error(http, 409, "STATE_CONFLICT"); }
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var digest = Convert.ToBase64String(SHA256.HashData(bytes));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(digest),
            Encoding.ASCII.GetBytes(upload.Sha256Base64)) || !ImageMagic(bytes, upload.ContentType))
        {
            File.Delete(path);
            return Error(http, 400, "VALIDATION_FAILED");
        }
        return Results.NoContent();
    }

    private static async Task<IResult> Complete(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, Guid uploadId, CancellationToken ct)
    {
        if (!Local(environment, config) && !S3(config))
            return Error(http, 503, "MEDIA_UNAVAILABLE");
        var user = await CurrentUser(http, db, ct);
        if (user is null || user.Status is not (UserStatus.PendingOnboarding or UserStatus.Active)) return Error(http, 403, "FORBIDDEN");
        var upload = await db.MediaUploads.AsNoTracking().SingleOrDefaultAsync(u => u.Id == uploadId && u.OwnerUserId == user.Id, ct);
        if (upload is null) return Error(http, 404, "NOT_FOUND");
        if (upload.Purpose == "PAYMENT_PROOF")
        {
            if (user.AccountType != AccountType.Customer || user.Status != UserStatus.Active) return Error(http, 403, "FORBIDDEN");
            if (!await db.Bookings.AnyAsync(x => x.Id == upload.BookingId && x.CustomerId == user.Id && x.VenueId == upload.VenueId, ct)) return Error(http, 404, "NOT_FOUND");
        }
        else if (user.AccountType != AccountType.VenueOperator) return Error(http, 403, "FORBIDDEN");
        if (upload.Status == "READY") return Results.Ok(new { data = new { upload.Id, upload.Status }, traceId = http.TraceIdentifier });
        if (S3(config))
        {
            try
            {
                var client = http.RequestServices.GetRequiredService<IAmazonS3>();
                var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                    { BucketName = config["Media:S3Bucket"], Key = upload.ObjectKey, ChecksumMode = ChecksumMode.ENABLED }, ct);
                if (metadata.ContentLength != upload.SizeBytes || metadata.Headers.ContentType != upload.ContentType ||
                    metadata.ChecksumSHA256 != upload.Sha256Base64)
                    return Error(http, 409, "UPLOAD_MISMATCH");
                using var response = await client.GetObjectAsync(config["Media:S3Bucket"], upload.ObjectKey, ct);
                await using var memory = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memory, ct);
                var bytes = memory.ToArray();
                if (memory.Length != upload.SizeBytes ||
                    Convert.ToBase64String(SHA256.HashData(bytes)) != upload.Sha256Base64 ||
                    !ImageMagic(bytes, upload.ContentType))
                    return Error(http, 409, "UPLOAD_MISMATCH");
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            { return Error(http, 409, "UPLOAD_NOT_FOUND"); }
            catch (AmazonClientException) { return Error(http, 503, "MEDIA_UNAVAILABLE"); }
        }
        else
        {
            var path = Path.Combine(environment.ContentRootPath, ".media-local", upload.Id.ToString("N"));
            if (!File.Exists(path)) return Error(http, 409, "UPLOAD_NOT_FOUND");
            var bytes = await File.ReadAllBytesAsync(path, ct);
            if (bytes.LongLength != upload.SizeBytes || !ImageMagic(bytes, upload.ContentType) ||
                Convert.ToBase64String(SHA256.HashData(bytes)) != upload.Sha256Base64)
                return Error(http, 409, "UPLOAD_MISMATCH");
        }
        // Verify bytes first, then serialize only the READY transition and its audit.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        upload = await db.MediaUploads.FromSqlInterpolated($"SELECT * FROM media_uploads WHERE id={uploadId} AND owner_user_id={user.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (upload is null) return Error(http, 404, "NOT_FOUND");
        if (upload.Status == "READY") return Results.Ok(new { data = new { upload.Id, upload.Status }, traceId = http.TraceIdentifier });
        if (upload.Status != "PENDING") return Error(http, 409, "STATE_CONFLICT");
        upload.Status = "READY";
        db.AuditEvents.Add(new AuditEvent { ActorUserId = user.Id, Action = "media.upload_ready",
            EntityType = "media_upload", EntityId = upload.Id, CorrelationId = http.TraceIdentifier,
            CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(new { data = new { upload.Id, upload.Status }, traceId = http.TraceIdentifier });
    }

    private static async Task<IResult> View(HttpContext http, ShuttleBookDbContext db,
        IWebHostEnvironment environment, IConfiguration config, Guid uploadId, CancellationToken ct)
    {
        if (!Local(environment, config) && !S3(config)) return Error(http, 503, "MEDIA_UNAVAILABLE");
        var user = await CurrentUser(http, db, ct);
        if (user is null) return Error(http, 403, "FORBIDDEN");
        var upload = await db.MediaUploads.SingleOrDefaultAsync(u => u.Id == uploadId && u.Status == "READY", ct);
        if (upload is null) return Error(http, 404, "NOT_FOUND");
        var allowed = user.AccountType == AccountType.Admin && user.Status == UserStatus.Active ||
            user.AccountType == AccountType.VenueOperator && user.Status is UserStatus.PendingOnboarding or UserStatus.Active &&
            await (from venue in db.Venues join member in db.BusinessMemberships on venue.BusinessId equals member.BusinessId
                where venue.Id == upload.VenueId && member.UserId == user.Id && member.Role == "OWNER" &&
                    (member.Status == "PENDING" || member.Status == "ACTIVE") select member.Id).AnyAsync(ct);
        if (upload.Purpose == "PAYMENT_PROOF")
        {
            // Approval media access never grants access to a customer's private payment evidence.
            allowed = user.Status == UserStatus.Active &&
                (user.AccountType == AccountType.Customer && upload.OwnerUserId == user.Id &&
                    await db.Bookings.AnyAsync(x => x.Id == upload.BookingId && x.CustomerId == user.Id && x.VenueId == upload.VenueId, ct) ||
                 user.AccountType == AccountType.VenueOperator &&
                    await db.PaymentEvidence.AnyAsync(x => x.ProofUploadId == upload.Id && x.BookingId == upload.BookingId, ct) &&
                    await PaymentEndpoints.HasVenueScope(db, user.Id, upload.VenueId, ct));
        }
        if (!allowed) return Error(http, 404, "NOT_FOUND");
        byte[] bytes;
        if (S3(config))
        {
            try
            {
                var client = http.RequestServices.GetRequiredService<IAmazonS3>();
                using var response = await client.GetObjectAsync(config["Media:S3Bucket"], upload.ObjectKey, ct);
                await using var memory = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memory, ct);
                if (memory.Length > 5_242_880) return Error(http, 409, "UPLOAD_MISMATCH");
                bytes = memory.ToArray();
            }
            catch (AmazonClientException) { return Error(http, 503, "MEDIA_UNAVAILABLE"); }
        }
        else
        {
            var path = Path.Combine(environment.ContentRootPath, ".media-local", upload.Id.ToString("N"));
            if (!File.Exists(path)) return Error(http, 404, "NOT_FOUND");
            bytes = await File.ReadAllBytesAsync(path, ct);
        }
        if (bytes.LongLength != upload.SizeBytes || Convert.ToBase64String(SHA256.HashData(bytes)) != upload.Sha256Base64)
            return Error(http, 409, "UPLOAD_MISMATCH");
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        return Results.File(bytes, upload.ContentType);
    }

    private static string Sign(IConfiguration config, Guid id, long expires, string sha)
    {
        var key = config["Identity:JwtSigningKey"] ?? throw new InvalidOperationException("Missing local media signing key.");
        var derived = HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes("media-upload-local-v1"));
        return Convert.ToHexString(HMACSHA256.HashData(derived, Encoding.UTF8.GetBytes($"{id:N}:{expires}:{sha}")));
    }
    private static bool S3(IConfiguration config) =>
        string.Equals(config["Media:Mode"], "S3", StringComparison.OrdinalIgnoreCase);
    private static bool Local(IWebHostEnvironment environment, IConfiguration config) =>
        !S3(config) && (environment.IsDevelopment() || environment.IsEnvironment("Testing")) &&
        (config["Media:Mode"] is null || string.Equals(config["Media:Mode"], "Local", StringComparison.OrdinalIgnoreCase));
    private static bool ValidSha(string? value)
    {
        if (value?.Length != 44) return false;
        try { return Convert.FromBase64String(value).Length == 32; } catch (FormatException) { return false; }
    }
    private static bool ImageMagic(byte[] bytes, string type) => type switch
    {
        "image/png" => bytes.Length >= 45 &&
            bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) &&
            bytes.AsSpan(bytes.Length - 8, 4).SequenceEqual("IEND"u8),
        "image/jpeg" => bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8 &&
            bytes[^2] == 0xff && bytes[^1] == 0xd9,
        "image/webp" => bytes.Length >= 20 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) == bytes.Length - 8 &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
            (bytes.AsSpan(12, 4).SequenceEqual("VP8 "u8) || bytes.AsSpan(12, 4).SequenceEqual("VP8L"u8) ||
             bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8)),
        _ => false
    };
    private static async Task<User?> CurrentUser(HttpContext http, ShuttleBookDbContext db, CancellationToken ct) =>
        Guid.TryParse(http.User.FindFirstValue("sub"), out var id) ? await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct) : null;
    private static IResult Error(HttpContext http, int status, string code) => CustomerRegistrationEndpoints.Problem(http, status, code);
}
