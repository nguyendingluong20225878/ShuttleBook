using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using ShuttleBook.Infrastructure.Identity;

namespace ShuttleBook.Api.Identity;

public sealed class ContactAttemptLimiter(IConfiguration configuration) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10_000 });
    private readonly object gate = new();
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool TryAcquire(string purpose, string contactType, string contact, int limit, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (!ContactNormalizer.TryNormalize(contactType, contact, out _, out var normalized)) return true;
        var pepper = configuration["Identity:OtpPepper"] ??
            throw new InvalidOperationException("Identity OTP pepper is not configured.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        var key = purpose + ":" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized)));
        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            if (!cache.TryGetValue(key, out AttemptWindow? current) || current is null || current.ExpiresAt <= now)
            {
                cache.Set(key, new AttemptWindow(1, now.Add(Window)), new MemoryCacheEntryOptions
                {
                    AbsoluteExpiration = now.Add(Window), Size = 1
                });
                return true;
            }
            if (current.Count >= limit)
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((current.ExpiresAt - now).TotalSeconds));
                return false;
            }
            current.Count++;
            return true;
        }
    }

    public void Dispose() => cache.Dispose();
    private sealed class AttemptWindow(int count, DateTimeOffset expiresAt)
    {
        public int Count { get; set; } = count;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
    }
}
