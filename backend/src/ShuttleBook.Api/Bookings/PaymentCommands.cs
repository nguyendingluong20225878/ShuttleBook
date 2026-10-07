using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Api.Bookings;

public static partial class PaymentEndpoints
{
    private sealed record PaymentCommand(string? BankReference = null, Guid? ProofUploadId = null,
        string? Note = null, long? ConfirmedAmount = null, string? Resolution = null,
        string? ReasonCode = null, string? Reason = null);

    private static Task<IResult> Report(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct) =>
        ExecuteCommand(http, db, clock, id, "TRANSFER_REPORT", ct);
    private static Task<IResult> Confirm(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct) =>
        ExecuteCommand(http, db, clock, id, "PAYMENT_CONFIRM", ct);
    private static Task<IResult> Decide(HttpContext http, ShuttleBookDbContext db, TimeProvider clock, Guid id, CancellationToken ct) =>
        ExecuteCommand(http, db, clock, id, "PAYMENT_DECIDE", ct);

    private static async Task<IResult> ExecuteCommand(HttpContext http, ShuttleBookDbContext db,
        TimeProvider clock, Guid id, string operation, CancellationToken ct)
    {
        var customerAction = operation == "TRANSFER_REPORT";
        var requiredType = customerAction ? AccountType.Customer : AccountType.VenueOperator;
        var (actor, authError) = await CurrentUser(http, db, requiredType, ct);
        if (authError is not null) return authError;
        var scope = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (scope is null || (customerAction ? scope.CustomerId != actor!.Id : !await HasVenueScope(db, actor!.Id, scope.VenueId, ct)))
            return Error(http, 404, "NOT_FOUND");
        if (http.Request.Query.Count != 0) return Error(http, 400, "VALIDATION_FAILED");
        var keys = http.Request.Headers["Idempotency-Key"];
        if (keys.Count != 1 || string.IsNullOrEmpty(keys[0]) || keys[0]!.Length > 128 ||
            keys[0]!.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            return Error(http, 400, "VALIDATION_FAILED");
        var versions = http.Request.Headers.IfMatch;
        if (versions.Count == 0) return Error(http, 428, "PRECONDITION_REQUIRED");
        var versionText = versions.Count == 1 ? versions[0] : null;
        if (versionText is null || versionText.Length < 3 || versionText[0] != '"' || versionText[^1] != '"' ||
            !long.TryParse(versionText.AsSpan(1, versionText.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            return Error(http, 400, "VALIDATION_FAILED");
        var (input, validationError) = await ReadCommand(http, operation, ct);
        if (validationError is not null) return validationError;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { bookingId = id, input }, Json))));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lockKey = $"{operation}:{actor!.Id}:{keys[0]}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))", ct);
        var booking = await db.Bookings.FromSqlInterpolated($"SELECT * FROM bookings WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (booking is null) return Error(http, 404, "NOT_FOUND");
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id={actor.Id} FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null || user.Status != UserStatus.Active) return Error(http, 401, "UNAUTHORIZED");
        if (user.AccountType != requiredType) return Error(http, 403, "FORBIDDEN");
        if (customerAction)
        {
            if (booking.CustomerId != actor.Id) return Error(http, 404, "NOT_FOUND");
        }
        else
        {
            // Lock current business/membership so suspension or revocation cannot pass this decision.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT b.id FROM businesses b JOIN venues v ON v.business_id=b.id WHERE v.id={booking.VenueId} FOR SHARE OF b", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT m.id FROM business_memberships m JOIN venues v ON v.business_id=m.business_id WHERE v.id={booking.VenueId} AND m.user_id={actor.Id} FOR SHARE OF m", ct);
            if (!await HasVenueScope(db, actor.Id, booking.VenueId, ct)) return Error(http, 404, "NOT_FOUND");
        }
        var previous = await db.BookingIdempotency.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor.Id && x.Operation == operation && x.Key == keys[0], ct);
        if (previous is not null)
        {
            if (previous.BookingId != id || previous.RequestHash != hash) return Error(http, 409, "IDEMPOTENCY_KEY_REUSED");
            return Ok(http, await BookingReadModel.Data(db, id, !customerAction, ct, clock.GetUtcNow()));
        }
        if (booking.Version != version) return Error(http, 412, "PRECONDITION_FAILED");
        var payment = await db.BookingPayments.FromSqlInterpolated($"SELECT * FROM payments WHERE booking_id={id} FOR UPDATE").SingleAsync(ct);
        var now = clock.GetUtcNow(); // Must be sampled after waiting for the booking lock.
        var reported = booking.Status == "AWAITING_OWNER_CONFIRMATION" && payment.Status == "TRANSFER_REPORTED";
        var review = booking.Status == "NEEDS_REVIEW" && payment.Status == "NEEDS_REVIEW";
        string eventType, auditAction;
        string payload = "{}";
        if (customerAction)
        {
            var initial = booking.Status == "AWAITING_TRANSFER" && payment.Status == "AWAITING_TRANSFER";
            if (!initial && !review) return Error(http, 409, "STATE_CONFLICT");
            if (initial && now >= booking.PaymentDeadline) return Error(http, 409, "PAYMENT_DEADLINE_EXPIRED");
            if (input!.ProofUploadId is Guid proofId)
            {
                var upload = await db.MediaUploads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == proofId, ct);
                if (upload is null || upload.Purpose != "PAYMENT_PROOF" || upload.BookingId != id || upload.OwnerUserId != actor.Id || upload.VenueId != booking.VenueId)
                    return Error(http, 404, "NOT_FOUND");
                if (upload.Status != "READY") return Error(http, 409, "UPLOAD_NOT_READY");
                if (await db.PaymentEvidence.AnyAsync(x => x.ProofUploadId == proofId, ct)) return Error(http, 409, "UPLOAD_MISMATCH");
            }
            db.PaymentEvidence.Add(new PaymentEvidence { PaymentId = payment.Id, BookingId = id, CustomerId = actor.Id,
                VenueId = booking.VenueId, ProofUploadId = input.ProofUploadId, Kind = initial ? "INITIAL" : "SUPPLEMENT",
                BankReference = input.BankReference, Note = input.Note, ReportedAt = now });
            booking.Status = "AWAITING_OWNER_CONFIRMATION"; payment.Status = "TRANSFER_REPORTED";
            payment.FirstReportedAt ??= now; payment.LastReportedAt = now;
            eventType = initial ? "PAYMENT_TRANSFER_REPORTED" : "PAYMENT_EVIDENCE_SUPPLEMENTED";
            auditAction = initial ? "payment.transfer_reported" : "payment.evidence_supplemented";
        }
        else
        {
            if (!reported && !review) return Error(http, 409, "STATE_CONFLICT");
            if (operation == "PAYMENT_CONFIRM")
            {
                if (input!.ConfirmedAmount != payment.ExpectedAmount) return Error(http, 409, "PAYMENT_AMOUNT_MISMATCH");
                booking.Status = "CONFIRMED"; payment.Status = "PAID";
                payment.ConfirmedAmount = input.ConfirmedAmount; payment.ConfirmedBy = actor.Id; payment.ConfirmedAt = now;
                eventType = "PAYMENT_CONFIRMED"; auditAction = "payment.confirmed";
                db.PaymentDecisions.Add(new PaymentDecision { PaymentId = payment.Id, BookingId = id, ActorUserId = actor.Id,
                    Resolution = "CONFIRMED", ConfirmedAmount = input.ConfirmedAmount, BankReference = input.BankReference,
                    Note = input.Note, DecidedAt = now });
            }
            else
            {
                if (input!.Resolution == "NEEDS_REVIEW")
                {
                    if (!reported) return Error(http, 409, "STATE_CONFLICT");
                    booking.Status = "NEEDS_REVIEW"; payment.Status = "NEEDS_REVIEW";
                    eventType = "PAYMENT_NEEDS_REVIEW"; auditAction = "payment.needs_review";
                }
                else
                {
                    var allocation = await db.CourtAllocations.FromSqlInterpolated($"SELECT * FROM court_allocations WHERE id={booking.AllocationId} FOR UPDATE").SingleAsync(ct);
                    booking.Status = "PAYMENT_REJECTED"; payment.Status = "REJECTED";
                    allocation.Status = "RELEASED"; allocation.ReleasedAt = now;
                    eventType = "PAYMENT_REJECTED"; auditAction = "payment.rejected";
                }
                payload = JsonSerializer.Serialize(new { reason = input.Reason }, Json);
                db.PaymentDecisions.Add(new PaymentDecision { PaymentId = payment.Id, BookingId = id, ActorUserId = actor.Id,
                    Resolution = input.Resolution!, ReasonCode = input.ReasonCode, Reason = input.Reason, DecidedAt = now });
            }
        }
        booking.Version++;
        db.BookingIdempotency.Add(new BookingIdempotency { ActorUserId = actor.Id, Operation = operation, Key = keys[0]!,
            BookingId = id, RequestHash = hash, CreatedAt = now });
        db.AuditEvents.Add(new AuditEvent { ActorUserId = actor.Id, Action = auditAction, EntityType = "booking",
            EntityId = id, CorrelationId = http.TraceIdentifier, CreatedAt = now });
        db.OutboxMessages.Add(new OutboxMessage { EventType = eventType, EntityId = id, TargetUserId = customerAction ? null : booking.CustomerId,
            Payload = payload, CreatedAt = now, NextAttemptAt = now });
        await db.SaveChangesAsync(ct);
        var result = await BookingReadModel.Data(db, id, !customerAction, ct, now);
        await tx.CommitAsync(ct);
        return Ok(http, result);
    }

    private static async Task<(PaymentCommand?, IResult?)> ReadCommand(HttpContext http, string operation, CancellationToken ct)
    {
        var allowed = operation switch
        {
            "TRANSFER_REPORT" => new[] { "bankReference", "proofUploadId", "note" },
            "PAYMENT_CONFIRM" => new[] { "confirmedAmount", "bankReference", "note" },
            _ => new[] { "resolution", "reasonCode", "reason" }
        };
        var bytes = new byte[8193]; var length = 0;
        while (length < bytes.Length)
        {
            var read = await http.Request.Body.ReadAsync(bytes.AsMemory(length), ct);
            if (read == 0) break;
            length += read;
        }
        if (length == bytes.Length) return (null, Error(http, 400, "VALIDATION_FAILED"));
        try
        {
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, Error(http, 400, "VALIDATION_FAILED"));
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in root.EnumerateObject())
            {
                if (!names.Add(p.Name)) return (null, Error(http, 400, "VALIDATION_FAILED"));
                if (!allowed.Contains(p.Name)) return (null, Error(http, 400, "UNSUPPORTED_FIELD"));
            }
            string? Text(string name, int max, bool required)
            {
                if (!root.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
                { if (required) throw new JsonException(); return null; }
                if (p.ValueKind != JsonValueKind.String) throw new JsonException();
                var value = p.GetString()!.Trim();
                if (value.Length > max || (required && value.Length == 0)) throw new JsonException();
                return value.Length == 0 ? null : value;
            }
            if (operation == "PAYMENT_DECIDE")
            {
                var resolution = Text("resolution", 32, true); var code = Text("reasonCode", 32, true);
                if (resolution is not ("NEEDS_REVIEW" or "FINAL_REJECTION") || code is not ("EVIDENCE_REQUIRED" or "AMOUNT_MISMATCH" or "REFERENCE_MISMATCH" or "TRANSACTION_NOT_FOUND" or "OTHER")) throw new JsonException();
                return (new PaymentCommand(Resolution: resolution, ReasonCode: code, Reason: Text("reason", 1000, true)), null);
            }
            var reference = Text("bankReference", 100, false); var note = Text("note", 1000, false);
            if (operation == "PAYMENT_CONFIRM")
            {
                if (!root.TryGetProperty("confirmedAmount", out var p) || p.ValueKind != JsonValueKind.Number || !p.TryGetInt64(out var amount) || amount is < 0 or > 999_999_999_999_999_999) throw new JsonException();
                return (new PaymentCommand(BankReference: reference, Note: note, ConfirmedAmount: amount), null);
            }
            Guid? proof = null;
            if (root.TryGetProperty("proofUploadId", out var upload) && upload.ValueKind != JsonValueKind.Null)
            { if (upload.ValueKind != JsonValueKind.String || !upload.TryGetGuid(out var value) || value == Guid.Empty) throw new JsonException(); proof = value; }
            return (new PaymentCommand(BankReference: reference, ProofUploadId: proof, Note: note), null);
        }
        catch (JsonException) { return (null, Error(http, 400, "VALIDATION_FAILED")); }
    }
}
