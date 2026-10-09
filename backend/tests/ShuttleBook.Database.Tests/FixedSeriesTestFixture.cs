using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Infrastructure.Bookings;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class FixedSeriesTests
{
    private static object Decision(string resolution, string reason, string reasonCode = "EVIDENCE_REQUIRED") => new { resolution, reasonCode, reason };
    private static async Task DrainOutbox(Fixture f)
    {
        for (var i = 0; i < 100; i++) { await using var db = f.Context(); if (!await OutboxDispatch.ProcessOne(db, f.Clock.GetUtcNow(), CancellationToken.None)) return; }
        Assert.Fail("Outbox did not drain within the test safety bound.");
    }

    private static async Task WaitForBookingLock(Fixture f, int expectedWaiters = 1)
        => await WaitForLock(f, "bookings", "FOR UPDATE", expectedWaiters);

    private static async Task WaitForLock(Fixture f, string resource, string operation, int expectedWaiters = 1)
    {
        await using var inspector = new NpgsqlConnection(f.Connection); await inspector.OpenAsync();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND wait_event_type='Lock' AND query ILIKE @resource AND query ILIKE @operation", inspector);
            command.Parameters.AddWithValue("resource", "%" + resource + "%"); command.Parameters.AddWithValue("operation", "%" + operation + "%");
            if ((long)(await command.ExecuteScalarAsync())! >= expectedWaiters) return;
            await Task.Delay(20);
        }
        Assert.Fail($"Expected {expectedWaiters} request(s) waiting for {resource} {operation}; the PostgreSQL lock barrier was not reached.");
    }

    private static string ReportRoute(Guid id) => $"/api/v1/bookings/{id}/transfer-evidence";
    private static string ConfirmRoute(Guid id) => $"/api/v1/operator/bookings/{id}/confirm-payment";
    private static string RejectRoute(Guid id) => $"/api/v1/operator/bookings/{id}/reject-payment";
    private static object Evidence(string reference = "DEMO-REFERENCE", Guid? proof = null, string? note = "Test only") =>
        new { bankReference = reference, proofUploadId = proof, note };
    private static object Confirmation(JsonElement created) => new { confirmedAmount = created.GetProperty("amount").GetInt64(), bankReference = "DEMO-OWNER-REFERENCE", note = "Reconciled test transaction" };
    private static Task<HttpResponseMessage> Send(HttpClient client, string route, string key, long? version, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request);
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response, int status = 200)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            return json.RootElement.GetProperty("data").Clone();
        }
    }
    private static async Task Code(HttpResponseMessage response, int status, string code)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
            using var json = JsonDocument.Parse(text);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
    }
    private static async Task<long> Count(ShuttleBookDbContext db, string table)
    {
        // Only fixed test-owned table names are accepted; never interpolate user input.
        Assert.Contains(table, new[] { "payment_evidence", "payment_decisions" });
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        return (long)(await new NpgsqlCommand($"SELECT count(*) FROM {table}", connection).ExecuteScalarAsync())!;
    }

    private sealed class ManualClock : TimeProvider
    {
        // PostgreSQL stores microseconds. A whole-second starting clock avoids
        // response-vs-persisted sub-microsecond differences at the deadline.
        private long utcTicks = DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
        public void Set(DateTimeOffset now) => Interlocked.Exchange(ref utcTicks, now.UtcTicks);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Connection = "";
        public string Name = "";
        private NpgsqlConnection? control;
        private readonly List<string> mediaPaths = [];
        public ApiFactory Factory = null!;
        public ManualClock Clock = new();
        public Guid VenueId, CourtId, CustomerId, OtherCustomerId, OwnerId, OwnerBId, BusinessId, QrId;
        public string Date = "";
        private object? lastCreateBody;
        private string lastCreateKey = "";
        public readonly byte[] Bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
        public ShuttleBookDbContext Context() => new(new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(Connection).Options);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var settings = LocalPostgresTestServer.Require(Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING")!);
            settings.Timeout = 15; settings.CommandTimeout = 30;
            f.Name = $"shuttlebook_f07_test_{Guid.NewGuid():N}";
            Assert.Matches("^shuttlebook_f07_test_[0-9a-f]{32}$", f.Name);
            f.control = new(settings.ConnectionString); await f.control.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(f.Name)}", f.control).ExecuteNonQueryAsync();
            settings.Database = f.Name; f.Connection = settings.ConnectionString;
            try
            {
                await using (var db = f.Context())
                {
                    await db.Database.MigrateAsync();
                    var now = f.Clock.GetUtcNow(); var hasher = new PasswordHasher<User>();
                    foreach (var name in new[] { "customer", "other", "owner", "owner-b", "admin" })
                    {
                        var user = new User { Email = $"f07-{name}@example.test", NormalizedEmail = $"f07-{name}@example.test",
                            AccountType = name == "admin" ? AccountType.Admin : name.StartsWith("owner", StringComparison.Ordinal) ? AccountType.VenueOperator : AccountType.Customer,
                            Status = UserStatus.Active, CreatedAt = now, UpdatedAt = now, EmailVerifiedAt = now };
                        user.PasswordHash = hasher.HashPassword(user, "Test-password-2026!"); db.Users.Add(user);
                        switch (name) { case "customer": f.CustomerId = user.Id; break; case "other": f.OtherCustomerId = user.Id; break; case "owner": f.OwnerId = user.Id; break; case "owner-b": f.OwnerBId = user.Id; break; }
                    }
                    var business = new Business { Name = "F07 Club", LegalName = "Test", Contact = "Private", Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                    var otherBusiness = new Business { Name = "F07 Other Club", LegalName = "Test", Contact = "Private", Status = "ACTIVE", CreatedAt = now, UpdatedAt = now };
                    db.Businesses.AddRange(business, otherBusiness); f.BusinessId = business.Id;
                    db.BusinessMemberships.AddRange(new BusinessMembership { BusinessId = business.Id, UserId = f.OwnerId, Role = "OWNER", Status = "ACTIVE", CreatedAt = now },
                        new BusinessMembership { BusinessId = otherBusiness.Id, UserId = f.OwnerBId, Role = "OWNER", Status = "ACTIVE", CreatedAt = now });
                    var venue = new Venue { BusinessId = business.Id, Name = "F07 Venue", Address = "Hà Nội", Contact = "Venue test", Latitude = 21, Longitude = 105,
                        Timezone = "Asia/Ho_Chi_Minh", Status = "PUBLISHED", CreatedAt = now, UpdatedAt = now };
                    db.Venues.Add(venue); f.VenueId = venue.Id;
                    var court = new Court { VenueId = venue.Id, Name = "F07 Court", Status = "ACTIVE", CreatedAt = now };
                    db.Courts.Add(court); f.CourtId = court.Id;
                    for (var day = 0; day < 7; day++)
                    {
                        db.CourtOperatingHours.Add(new CourtOperatingHour { CourtId = court.Id, DayOfWeek = day, OpensAt = new(17, 0), ClosesAt = new(22, 0) });
                        db.PricingRules.Add(new PricingRule { CourtId = court.Id, DayOfWeek = day, StartsAt = new(17, 0), EndsAt = new(22, 0), PricePerSlot = 100000 });
                    }
                    f.Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(venue.Timezone)).Date).AddDays(7).ToString("yyyy-MM-dd");
                    var qr = new MediaUpload { OwnerUserId = f.OwnerId, VenueId = venue.Id, Purpose = "QR", Status = "READY", ObjectKey = $"f07/{Guid.NewGuid():N}",
                        ContentType = "image/png", SizeBytes = f.Bytes.Length, Sha256Base64 = Convert.ToBase64String(SHA256.HashData(f.Bytes)), CreatedAt = now };
                    db.MediaUploads.Add(qr); f.QrId = qr.Id;
                    db.VenuePaymentAccounts.Add(new VenuePaymentAccount { VenueId = venue.Id, BankCode = "TEST", AccountName = "TEST CLUB", AccountNumber = "1234567890", QrUploadId = qr.Id });
                    await db.SaveChangesAsync(); await db.Database.MigrateAsync();
                }
                f.Factory = new(f.Connection, f.Clock);
                using var client = f.Factory.CreateClient();
                var root = f.Factory.Services.GetRequiredService<IWebHostEnvironment>();
                var path = Path.Combine(root.ContentRootPath, ".media-local", f.QrId.ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllBytesAsync(path, f.Bytes); f.mediaPaths.Add(path);
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        public async Task<HttpClient> Client(string name) { var client = Factory.CreateClient(); await Login(client, name); return client; }
        public async Task Login(HttpClient client, string name)
        {
            // F01 isolates Admin authentication from the public Customer/operator login.
            var route = name == "admin" ? "/api/v1/admin-auth/login" : "/api/v1/auth/login";
            if (name == "admin") client.DefaultRequestHeaders.Add("Origin", "http://localhost:5175");
            var result = await Data(await client.PostAsJsonAsync(route, new { contactType = "email", contact = $"f07-{name}@example.test", password = "Test-password-2026!" }));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.GetProperty("accessToken").GetString());
        }
        public async Task<JsonElement> Book(HttpClient client, string start = "18:00", string end = "19:00", string key = "booking", string? date = null)
        {
            var q = await Data(await client.PostAsJsonAsync("/api/v1/availability/quote", new { courtId = CourtId, date = date ?? Date, startsAt = start, endsAt = end }));
            lastCreateBody = new { courtId = CourtId, quoteId = q.GetProperty("quoteId").GetGuid(), startsAt = q.GetProperty("startsAt").GetDateTimeOffset(), endsAt = q.GetProperty("endsAt").GetDateTimeOffset() };
            lastCreateKey = key;
            return await Data(await ReplayCreate(client), 201);
        }
        public Task<HttpResponseMessage> ReplayCreate(HttpClient client) => Send(client, "/api/v1/bookings", lastCreateKey, null, lastCreateBody!);
        public async Task<JsonElement> PresignProof(HttpClient client, Guid bookingId)
        {
            var upload = await Data(await client.PostAsJsonAsync($"/api/v1/bookings/{bookingId}/proof-uploads/presign", new {
                contentType = "image/png", sizeBytes = Bytes.Length, sha256Base64 = Convert.ToBase64String(SHA256.HashData(Bytes)) }));
            var id = upload.GetProperty("id").GetGuid();
            mediaPaths.Add(Path.Combine(Factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath, ".media-local", id.ToString("N")));
            return upload;
        }
        public async Task<Guid> UploadProof(HttpClient client, Guid bookingId)
        {
            var upload = await PresignProof(client, bookingId); var id = upload.GetProperty("id").GetGuid();
            using var content = new ByteArrayContent(Bytes); content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            using var put = await client.PutAsync(upload.GetProperty("uploadUrl").GetString(), content); Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
            await Data(await client.PostAsync($"/api/v1/uploads/{id}/complete", null));
            await Data(await client.PostAsync($"/api/v1/uploads/{id}/complete", null));
            return id;
        }
        public async ValueTask DisposeAsync()
        {
            Factory?.Dispose(); foreach (var path in mediaPaths) if (File.Exists(path)) File.Delete(path);
            if (control is null) return;
            var settings = LocalPostgresTestServer.Require(control.ConnectionString);
            Assert.Matches("^shuttlebook_f07_test_[0-9a-f]{32}$", Name);
            Assert.Equal(Name, new NpgsqlConnectionStringBuilder(Connection).Database);
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {new NpgsqlCommandBuilder().QuoteIdentifier(Name)} WITH (FORCE)", control).ExecuteNonQueryAsync();
            await control.DisposeAsync();
        }
    }
    private sealed class ApiFactory(string connection, ManualClock clock) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:ShuttleBook"] = connection, ["Identity:JwtSigningKey"] = "f07-test-signing-key-more-than-32-bytes",
                ["Identity:OtpPepper"] = "f07-test-pepper-more-than-32-bytes", ["Media:Mode"] = "Local",
                ["AdminSession:AllowedOrigin"] = "http://localhost:5175" }));
            builder.ConfigureLogging(l => l.ClearProviders()); return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); });
        }
    }
}
