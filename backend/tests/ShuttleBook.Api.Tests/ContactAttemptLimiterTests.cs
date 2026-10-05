using Microsoft.Extensions.Configuration;
using ShuttleBook.Api.Identity;

namespace ShuttleBook.Api.Tests;

public sealed class ContactAttemptLimiterTests
{
    [Fact]
    public void Contact_limit_opens_again_after_its_window()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:OtpPepper"] = "test-only-otp-pepper-32-bytes-minimum"
        }).Build();
        var clock = new TestClock();
        using var limiter = new ContactAttemptLimiter(configuration, clock);
        for (var attempt = 0; attempt < 3; attempt++)
            Assert.True(limiter.TryAcquire("register", "email", "f011-window@example.test", 3, out _));
        Assert.False(limiter.TryAcquire("register", "email", "F011-Window@Example.Test", 3, out var retryAfter));
        Assert.InRange(retryAfter, 1, 900);
        clock.Advance(TimeSpan.FromMinutes(15));
        Assert.True(limiter.TryAcquire("register", "email", "f011-window@example.test", 3, out _));
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
