using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;
using ShuttleBook.Infrastructure.Onboarding;

namespace ShuttleBook.Database.Tests;

public sealed partial class OnboardingFlowTests
{
    private static async Task<HttpResponseMessage> DraftSchedulePut(HttpClient owner, Guid court,
        object body, long version)
    {
        using var request = VersionedPut($"{Root}/courts/{court}/schedule", body, version);
        return await owner.SendAsync(request);
    }

    [Fact]
    public async Task Schedule_configuration_draft_read_scope_and_version_prevent_lost_updates()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, venue, _) = await ActiveInventory(context);
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            using var other = await Login(anonymous, factory, "addition-other@example.test");
            using var customer = await Login(anonymous, factory, "addition-customer@example.test");
            var court = (await Data(await owner.PostAsJsonAsync($"{Root}/venues/{venue}/courts",
                new { name = "1" }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
            var url = $"{Root}/courts/{court}/schedule";
            await Problem(await anonymous.GetAsync(url), HttpStatusCode.Unauthorized, "UNAUTHORIZED");
            await Problem(await other.GetAsync(url), HttpStatusCode.NotFound, "NOT_FOUND");
            await Problem(await customer.GetAsync(url), HttpStatusCode.Forbidden, "FORBIDDEN");
            await Problem(await owner.GetAsync($"{url}?version=1"), HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            using (var response = await owner.GetAsync(url))
            {
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
                Assert.Equal("\"1\"", response.Headers.ETag!.ToString());
                var draft = await Data(response);
                Assert.Equal("INACTIVE", draft.GetProperty("status").GetString());
                Assert.Empty(draft.GetProperty("hours").EnumerateArray());
                Assert.Empty(draft.GetProperty("rules").EnumerateArray());
            }
            await Problem(await owner.PutAsJsonAsync(url, ScheduleBody()), (HttpStatusCode)428, "PRECONDITION_REQUIRED");
            var results = await Task.WhenAll(DraftSchedulePut(owner, court, ScheduleBody(), 1),
                DraftSchedulePut(owner, court, ScheduleBody(), 1));
            var saved = await Data(Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK));
            Assert.Equal(2, saved.GetProperty("version").GetInt64());
            await Problem(Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict),
                HttpStatusCode.Conflict, "STATE_CONFLICT");
            var hydrated = await Data(await owner.GetAsync(url));
            Assert.Equal(2, hydrated.GetProperty("version").GetInt64());
            Assert.Equal("ACTIVE", hydrated.GetProperty("status").GetString());
            Assert.Equal("08:00:00", Assert.Single(hydrated.GetProperty("hours").EnumerateArray())
                .GetProperty("opensAt").GetString());
            Assert.Equal(100000, Assert.Single(hydrated.GetProperty("basePrices").EnumerateArray())
                .GetProperty("pricePerSlot").GetInt64());
            await using (var db = context())
            {
                (await db.Courts.FindAsync(court))!.Status = "PENDING_APPROVAL";
                await db.SaveChangesAsync();
            }
            Assert.Equal("PENDING_APPROVAL", (await Data(await owner.GetAsync(url))).GetProperty("status").GetString());
            await Problem(await DraftSchedulePut(owner, court, ScheduleBody(), 2), HttpStatusCode.Conflict, "STATE_CONFLICT");
            await using var check = context();
            Assert.Equal(2, (await check.Courts.FindAsync(court))!.Version);
            Assert.Equal(1, await check.AuditEvents.CountAsync(a => a.EntityId == court && a.Action == "court.schedule_updated"));
        });
    }

    [Fact]
    public async Task Schedule_configuration_preview_accepts_thirty_minute_steps_and_matches_customer_quotes()
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, _, court) = await ActiveInventory(context);
            await using (var db = context())
            {
                foreach (var minutes in new[] { 120, 150, 180 })
                    db.Users.Add(User($"preview-{minutes}@example.test", AccountType.Customer, UserStatus.Active));
                await db.SaveChangesAsync();
            }
            await using var factory = new OnboardingApiFactory(connection);
            using var anonymous = factory.CreateClient();
            using var owner = await Login(anonymous, factory, "addition-owner@example.test");
            var url = $"/api/v1/operator/courts/{court}";
            using (var schedule = VersionedPut($"{url}/schedule", new
            {
                hours = new[] { new { dayOfWeek = 1, opensAt = "08:00", closesAt = "12:00" } },
                prices = new[] { new { dayOfWeek = 1, startsAt = "08:00", endsAt = "12:00", pricePerSlot = 100000 } }
            }, 1)) await Data(await owner.SendAsync(schedule));
            using (var policy = VersionedPut($"{url}/booking-policy", new
            { bookingBlockMinutes = 60, minimumBookingMinutes = 120, holdMinutes = 20 }, 2))
                await Data(await owner.SendAsync(policy));
            var monday = NextMonday();
            using (var rules = VersionedPut($"{url}/pricing-rules", new
            {
                rules = new[] { new { startsOn = monday, endsOn = monday.AddDays(21), dayOfWeek = 1,
                    startsAt = "08:30", endsAt = "09:30", pricePerSlot = 300000, priority = 2 } }
            }, 3)) await Data(await owner.SendAsync(rules));
            var previewAmounts = new Dictionary<int, decimal>();
            for (var index = 0; index < 3; index++)
            {
                var minutes = 120 + index * 30;
                var date = monday.AddDays(index * 7).ToString("yyyy-MM-dd");
                var end = new TimeOnly(8, 0).AddMinutes(minutes).ToString("HH:mm");
                using var response = await owner.GetAsync($"{url}/price-preview?date={date}&startsAt=08:00&endsAt={end}");
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
                var preview = await Data(response);
                previewAmounts[minutes] = preview.GetProperty("totalPrice").GetDecimal();
                Assert.Equal(minutes / 30, preview.GetProperty("slots").GetArrayLength());
                Assert.Equal((minutes / 30 - 2) * 100000 + 600000, previewAmounts[minutes]);
            }
            await using (var db = context())
            {
                Assert.Empty(await db.BookingQuotes.ToListAsync());
                Assert.Empty(await db.QuoteReservations.ToListAsync());
                Assert.Empty(await db.CourtAllocations.ToListAsync());
            }
            for (var index = 0; index < 3; index++)
            {
                var minutes = 120 + index * 30;
                using var customer = await Login(anonymous, factory, $"preview-{minutes}@example.test");
                var quote = await Data(await customer.PostAsJsonAsync("/api/v1/availability/quote", new
                { courtId = court, date = monday.AddDays(index * 7).ToString("yyyy-MM-dd"), startsAt = "08:00",
                    endsAt = new TimeOnly(8, 0).AddMinutes(minutes).ToString("HH:mm") }));
                Assert.Equal(previewAmounts[minutes], quote.GetProperty("amount").GetDecimal());
                Assert.Equal(minutes / 30, quote.GetProperty("slots").GetArrayLength());
            }
            await Problem(await owner.GetAsync($"{url}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08:00&endsAt=09:30"),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
            await Problem(await owner.GetAsync($"{url}/price-preview?date={monday:yyyy-MM-dd}&startsAt=08:15&endsAt=10:30"),
                HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Schedule_configuration_get_returns_one_snapshot_during_concurrent_write(bool active)
    {
        await WithDatabaseAsync(async (connection, context) =>
        {
            var (_, _, court) = await ActiveInventory(context);
            await using (var db = context())
            {
                if (!active) (await db.Courts.FindAsync(court))!.Status = "INACTIVE";
                db.CourtOperatingHours.Add(new CourtOperatingHour { CourtId = court, DayOfWeek = 1,
                    OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(10, 0) });
                db.PricingRules.Add(new PricingRule { CourtId = court, DayOfWeek = 1,
                    StartsAt = new TimeOnly(8, 0), EndsAt = new TimeOnly(10, 0), PricePerSlot = 100000 });
                await db.SaveChangesAsync();
            }
            var interceptor = new ScheduleReadInterleaver();
            await using var factory = new ScheduleConfigurationApiFactory(connection, interceptor);
            using var anonymous = factory.CreateClient();
            var login = await Data(await anonymous.PostAsJsonAsync("/api/v1/auth/login", new
            { contactType = "email", contact = "addition-owner@example.test", password = Password }));
            using var owner = factory.CreateClient();
            owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
            var url = active ? $"/api/v1/operator/courts/{court}/operations" : $"{Root}/courts/{court}/schedule";
            interceptor.Arm();
            var read = owner.GetAsync(url);
            try
            {
                await interceptor.Reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await using var update = context();
                await using var tx = await update.Database.BeginTransactionAsync();
                var current = (await update.Courts.FindAsync(court))!;
                current.Version++;
                (await update.CourtOperatingHours.SingleAsync(h => h.CourtId == court)).ClosesAt = new TimeOnly(11, 0);
                var price = await update.PricingRules.SingleAsync(p => p.CourtId == court);
                price.EndsAt = new TimeOnly(11, 0); price.PricePerSlot = 500000;
                await update.SaveChangesAsync();
                await tx.CommitAsync();
            }
            finally { interceptor.Resume.TrySetResult(); }
            var before = await Data(await read);
            Assert.Equal(1, before.GetProperty("version").GetInt64());
            Assert.Equal("10:00:00", Assert.Single(before.GetProperty("hours").EnumerateArray()).GetProperty("closesAt").GetString());
            Assert.Equal(100000, Assert.Single(before.GetProperty("basePrices").EnumerateArray()).GetProperty("pricePerSlot").GetInt64());
            var after = await Data(await owner.GetAsync(url));
            Assert.Equal(2, after.GetProperty("version").GetInt64());
            Assert.Equal("11:00:00", Assert.Single(after.GetProperty("hours").EnumerateArray()).GetProperty("closesAt").GetString());
            Assert.Equal(500000, Assert.Single(after.GetProperty("basePrices").EnumerateArray()).GetProperty("pricePerSlot").GetInt64());
        });
    }

    private sealed class ScheduleReadInterleaver : DbCommandInterceptor
    {
        private int armed;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref armed, 1);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM court_operating_hours", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                Reached.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class ScheduleConfigurationApiFactory(string connection, ScheduleReadInterleaver interceptor)
        : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShuttleBook"] = connection,
                ["Identity:JwtSigningKey"] = "schedule-test-jwt-signing-key-more-than-32-bytes",
                ["Identity:OtpPepper"] = "schedule-test-otp-pepper-more-than-32-bytes"
            }));
            builder.ConfigureLogging(log => log.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddDbContext<ShuttleBookDbContext>(options => options.AddInterceptors(interceptor));
            });
        }
    }
}
