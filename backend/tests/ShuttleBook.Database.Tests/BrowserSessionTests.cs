using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShuttleBook.Api.Identity;
using ShuttleBook.Infrastructure.Data;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Database.Tests;

public sealed class BrowserSessionTests
{
    [Fact]
    public async Task Browser_cookie_contract_roles_origin_idle_absolute_and_revocation_use_postgis()
    {
        await using var f = await Fixture.Create();
        await using var factory = new Factory(f.Connection, f.Clock);
        using var client = factory.CreateClient(new() { HandleCookies = false });
        var credentials = new { contactType = "email", contact = "customer@session.test", password = Fixture.Password };
        var route = "/api/v1/browser-auth/customer/";
        var denied = await Post(client, route + "login", credentials, "https://evil.test");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.False(denied.Headers.Contains("Set-Cookie"));
        Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
        using (var bad = new HttpRequestMessage(HttpMethod.Post, route + "login") { Content = new StringContent("{}") })
        {
            bad.Headers.Add("Origin", "http://localhost:5173");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(bad)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, route + "login",
            new { contactType = "email", contact = "partner@session.test", password = Fixture.Password })).StatusCode);
        await using (var db = f.Context()) Assert.Empty(await db.RefreshSessions.ToListAsync());
        var login = await Post(client, route + "login", credentials);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookie = login.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", setCookie.ToLowerInvariant());
        Assert.Contains("path=/api/v1/browser-auth/customer", setCookie);
        Assert.Contains("secure", setCookie.ToLowerInvariant()); // Testing is not Development.
        var cookie = setCookie.Split(';')[0];
        var data = await Data(login);
        Assert.False(data.TryGetProperty("refreshToken", out _));
        Assert.Equal(600, data.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal(f.Clock.Now.AddDays(30), data.GetProperty("refreshExpiresAt").GetDateTimeOffset());
        Assert.Equal(f.Clock.Now.AddMinutes(30), data.GetProperty("idleExpiresAt").GetDateTimeOffset());
        using (var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me"))
        {
            me.Headers.Authorization = new("Bearer", data.GetProperty("accessToken").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);
        }
        f.Clock.Now = f.Clock.Now.AddMinutes(29);
        var restored = await Post(client, route + "restore", new { }, cookie: cookie);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False(restored.Headers.Contains("Set-Cookie"));
        Assert.Equal(data.GetProperty("idleExpiresAt").GetDateTimeOffset(), (await Data(restored)).GetProperty("idleExpiresAt").GetDateTimeOffset());
        var active = await Data(await Post(client, route + "activity", new { }, cookie: cookie));
        Assert.Equal(f.Clock.Now.AddMinutes(30), active.GetProperty("idleExpiresAt").GetDateTimeOffset());
        var wrongLogout = await Post(client, route + "logout", new { }, "http://localhost:5174", cookie);
        Assert.Equal(HttpStatusCode.Forbidden, wrongLogout.StatusCode);
        Assert.False(wrongLogout.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, route + "restore", new { }, cookie: cookie)).StatusCode);
        f.Clock.Now = f.Clock.Now.AddMinutes(30);
        var expiredActivity = await Post(client, route + "activity", new { }, cookie: cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredActivity.StatusCode);
        Assert.False(expiredActivity.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, route + "restore", new { }, cookie: cookie)).StatusCode);

        // Pending partner remains onboarding-only; customer cookie cannot restore in partner portal.
        var partner = await Post(client, "/api/v1/browser-auth/partner/login",
            new { contactType = "email", contact = "partner@session.test", password = Fixture.Password }, "http://localhost:5174");
        var partnerCookie = partner.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.Equal("PENDING_ONBOARDING", (await Data(partner)).GetProperty("user").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/v1/browser-auth/partner/restore", new { }, "http://localhost:5174",
            cookie.Replace("shuttlebook_customer_refresh=", "shuttlebook_partner_refresh="))).StatusCode);
        await using (var db = f.Context())
        {
            await db.Users.Where(u => u.NormalizedEmail == "partner@session.test")
                .ExecuteUpdateAsync(updates => updates.SetProperty(u => u.Status, UserStatus.Suspended));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/v1/browser-auth/partner/restore", new { }, "http://localhost:5174", partnerCookie)).StatusCode);

        var absoluteLogin = await Post(client, route + "login", credentials);
        var absoluteCookie = absoluteLogin.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var absoluteData = await Data(absoluteLogin);
        var expiry = absoluteData.GetProperty("refreshExpiresAt").GetDateTimeOffset();
        f.Clock.Now = expiry.AddMinutes(-1);
        await using (var db = f.Context())
            await db.RefreshSessions.Where(s => s.RevokedAt == null)
                .ExecuteUpdateAsync(updates => updates.SetProperty(s => s.LastActivityAt, f.Clock.Now));
        var nearEnd = await Data(await Post(client, route + "activity", new { }, cookie: absoluteCookie));
        Assert.Equal(expiry, nearEnd.GetProperty("idleExpiresAt").GetDateTimeOffset());
        Assert.Equal(expiry, nearEnd.GetProperty("refreshExpiresAt").GetDateTimeOffset());
        f.Clock.Now = expiry;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, route + "restore", new { }, cookie: absoluteCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, route + "logout", new { }, cookie: absoluteCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, route + "logout", new { })).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_refresh_logout_family_lock_barrier_revokes_replacement(bool refreshFirst)
    {
        await using var f = await Fixture.Create();
        AuthTokens first;
        await using (var db = f.Context()) first = (await f.Auth(db).LoginAsync("email", "customer@session.test", Fixture.Password, "login", default))!;
        var family = Family(first.AccessToken);
        await using var barrier = f.Context();
        await using var transaction = await barrier.Database.BeginTransactionAsync();
        await AuthSessionService.AcquireFamilyLockAsync(barrier, family, default);
        await using var refreshDb = f.Context();
        await using var logoutDb = f.Context();
        Task<AuthTokens?>? refresh = null;
        Task<bool>? logout = null;
        if (refreshFirst) refresh = f.Auth(refreshDb).RefreshAsync(first.RefreshToken, "refresh", default);
        else logout = f.Auth(logoutDb).LogoutAsync(first.User.Id, family, first.RefreshToken, "logout", default);
        await f.WaitForFamilyWaiters(1);
        if (refreshFirst) logout = f.Auth(logoutDb).LogoutAsync(first.User.Id, family, first.RefreshToken, "logout", default);
        else refresh = f.Auth(refreshDb).RefreshAsync(first.RefreshToken, "refresh", default);
        await f.WaitForFamilyWaiters(2);
        await transaction.CommitAsync();
        var rotated = await refresh!;
        Assert.True(await logout!);
        if (refreshFirst)
        {
            Assert.NotNull(rotated);
            Assert.Equal(first.RefreshExpiresAt, rotated.RefreshExpiresAt);
        }
        else Assert.Null(rotated);
        await using var check = f.Context();
        var sessions = await check.RefreshSessions.Where(s => s.FamilyId == family).ToListAsync();
        Assert.Equal(refreshFirst ? 2 : 1, sessions.Count);
        Assert.All(sessions, s => Assert.NotNull(s.RevokedAt));
        Assert.Null(await f.Auth(check).RefreshAsync((rotated ?? first).RefreshToken, "after-logout", default));
    }

    [Fact]
    public async Task Browser_parallel_restore_activity_and_logout_never_restore_revoked_family_or_touch_on_polling()
    {
        await using var f = await Fixture.Create();
        BrowserAuthTokens first;
        await using (var db = f.Context()) first = (await f.Auth(db).BrowserLoginAsync(AccountType.Customer, "email", "customer@session.test", Fixture.Password, "login", default))!;
        await using var barrier = f.Context();
        await using var tx = await barrier.Database.BeginTransactionAsync();
        await AuthSessionService.AcquireFamilyLockAsync(barrier, Family(first.Tokens.AccessToken), default);
        await using var logoutDb = f.Context();
        await using var restoreDb = f.Context();
        await using var activityDb = f.Context();
        var logout = f.Auth(logoutDb).LogoutBrowserAsync(AccountType.Customer, first.Tokens.RefreshToken, "logout", default);
        await f.WaitForFamilyWaiters(1);
        var restore = f.Auth(restoreDb).RestoreBrowserAsync(AccountType.Customer, first.Tokens.RefreshToken, false, "restore", default);
        var activity = f.Auth(activityDb).RestoreBrowserAsync(AccountType.Customer, first.Tokens.RefreshToken, true, "activity", default);
        await f.WaitForFamilyWaiters(3);
        await tx.CommitAsync();
        await logout;
        Assert.Null(await restore);
        Assert.Null(await activity);
    }

    [Fact]
    public async Task Browser_access_rejects_idle_family_without_polling_activity_updates()
    {
        await using var f = await Fixture.Create();
        BrowserAuthTokens first;
        await using (var db = f.Context()) first = (await f.Auth(db).BrowserLoginAsync(AccountType.Customer, "email", "customer@session.test", Fixture.Password, "login", default))!;
        await using var factory = new Factory(f.Connection, f.Clock);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", first.Tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await using (var db = f.Context())
        {
            Assert.Equal(f.Clock.Now, (await db.RefreshSessions.SingleAsync()).LastActivityAt);
            await db.RefreshSessions.ExecuteUpdateAsync(u => u.SetProperty(s => s.LastActivityAt, f.Clock.Now.AddMinutes(-30)));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    private static Guid Family(string token) => Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Single(c => c.Type == "sid").Value);
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }
    private static Task<HttpResponseMessage> Post(HttpClient client, string route, object body, string origin = "http://localhost:5173", string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add("Origin", origin);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Factory(string connection, Clock clock) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShuttleBook"] = connection, ["Identity:JwtSigningKey"] = Fixture.Key,
                ["BrowserSession:CustomerOrigin"] = "http://localhost:5173", ["BrowserSession:PartnerOrigin"] = "http://localhost:5174"
            }));
            builder.ConfigureLogging(l => l.ClearProviders());
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(s => { s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(clock); });
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal const string Password = "Session-Test-2026!";
        internal const string Key = "session-test-only-jwt-signing-key-32-bytes";
        internal readonly Clock Clock = new();
        internal string Connection = "";
        private NpgsqlConnection control = null!;
        private string quoted = "";
        internal ShuttleBookDbContext Context() => new(new DbContextOptionsBuilder<ShuttleBookDbContext>().UseNpgsql(Connection).Options);
        internal AuthSessionService Auth(ShuttleBookDbContext db) => new(db, new PasswordHasher<User>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:JwtSigningKey"] = Key }).Build(), Clock);
        internal static async Task<Fixture> Create()
        {
            var settings = LocalPostgresTestServer.Require(Environment.GetEnvironmentVariable("SHUTTLEBOOK_TEST_CONNECTION_STRING")!);
            // Docker's IPv4 loopback mapping can outlive a slow localhost IPv6 attempt;
            // allow bootstrap time while leaving concurrency barriers and assertions unchanged.
            if (settings.Host == "localhost") settings.Host = "127.0.0.1";
            settings.Pooling = false; settings.Timeout = 15; settings.CommandTimeout = 30;
            var f = new Fixture();
            f.control = new(settings.ConnectionString);
            try { await f.control.OpenAsync(); }
            catch { await f.control.DisposeAsync(); throw; }
            var name = "shuttlebook_session_test_" + Guid.NewGuid().ToString("N");
            f.quoted = new NpgsqlCommandBuilder().QuoteIdentifier(name);
            await using (var create = new NpgsqlCommand($"CREATE DATABASE {f.quoted}", f.control)) await create.ExecuteNonQueryAsync();
            settings.Database = name; f.Connection = settings.ConnectionString;
            try
            {
                await using var db = f.Context(); await db.Database.MigrateAsync();
                foreach (var (email, role, status) in new[] { ("customer@session.test", AccountType.Customer, UserStatus.Active), ("partner@session.test", AccountType.VenueOperator, UserStatus.PendingOnboarding) })
                {
                    var user = new User { Email = email, NormalizedEmail = email, AccountType = role, Status = status,
                        EmailVerifiedAt = f.Clock.Now, CreatedAt = f.Clock.Now, UpdatedAt = f.Clock.Now };
                    user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password); db.Users.Add(user);
                }
                await db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        internal async Task WaitForFamilyWaiters(int expected)
        {
            await using var inspector = new NpgsqlConnection(Connection); await inspector.OpenAsync();
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            while (DateTimeOffset.UtcNow < deadline)
            {
                await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%'", inspector);
                if ((long)(await command.ExecuteScalarAsync())! >= expected) return;
                await Task.Delay(20);
            }
            Assert.Fail("PostgreSQL family lock barrier not reached.");
        }
        public async ValueTask DisposeAsync()
        {
            await using (var drop = new NpgsqlCommand($"DROP DATABASE {quoted} WITH (FORCE)", control)) await drop.ExecuteNonQueryAsync();
            await control.DisposeAsync();
        }
    }
}
