using System.Collections.Concurrent;

namespace URLShortener.Services;

public record RateLimitRule(int PermitLimit, TimeSpan Window, TimeSpan BlockDuration);

public record RateLimitCheckResult(
    bool IsAllowed,
    bool IsBlocked,
    int Limit,
    int Remaining,
    int ResetSeconds,
    int RetryAfter,
    DateTimeOffset? BlockedUntil
);

public interface IRateLimiterService
{
    RateLimitCheckResult CheckRateLimit(string clientKey, string category);
}

public class RateLimiterService : IRateLimiterService
{
    private class ClientCategoryState
    {
        public readonly object Lock = new();
        public readonly List<DateTimeOffset> Timestamps = [];
        public DateTimeOffset? BlockedUntil;
    }

    private readonly ConcurrentDictionary<string, ClientCategoryState> _states = new();

    private readonly Dictionary<string, RateLimitRule> _rules = new(StringComparer.OrdinalIgnoreCase)
    {
        // Auth endpoints: 5 attempts per 60s, block for 60s if exceeded
        ["auth"] = new(PermitLimit: 5, Window: TimeSpan.FromSeconds(60), BlockDuration: TimeSpan.FromSeconds(60)),

        // Shorten endpoint: 10 attempts per 60s, block for 60s if exceeded
        ["shorten"] = new(PermitLimit: 10, Window: TimeSpan.FromSeconds(60), BlockDuration: TimeSpan.FromSeconds(60)),

        // General endpoints: 60 attempts per 60s, block for 60s if exceeded
        ["general"] = new(PermitLimit: 60, Window: TimeSpan.FromSeconds(60), BlockDuration: TimeSpan.FromSeconds(60))
    };

    public RateLimitCheckResult CheckRateLimit(string clientKey, string category)
    {
        if (!_rules.TryGetValue(category, out var rule))
        {
            rule = _rules["general"];
        }

        var fullKey = $"{category}:{clientKey}";
        var state = _states.GetOrAdd(fullKey, _ => new ClientCategoryState());

        var now = DateTimeOffset.UtcNow;

        lock (state.Lock)
        {
            // Check if already in a block
            if (state.BlockedUntil.HasValue)
            {
                if (state.BlockedUntil.Value > now)
                {
                    var retryAfterSeconds = (int)Math.Ceiling((state.BlockedUntil.Value - now).TotalSeconds);
                    return new RateLimitCheckResult(
                        IsAllowed: false,
                        IsBlocked: true,
                        Limit: rule.PermitLimit,
                        Remaining: 0,
                        ResetSeconds: retryAfterSeconds,
                        RetryAfter: Math.Max(1, retryAfterSeconds),
                        BlockedUntil: state.BlockedUntil.Value
                    );
                }
                else
                {
                    // Block expired, reset state
                    state.BlockedUntil = null;
                    state.Timestamps.Clear();
                }
            }

            // Remove timestamps outside current window
            var windowStart = now - rule.Window;
            state.Timestamps.RemoveAll(ts => ts <= windowStart);

            if (state.Timestamps.Count >= rule.PermitLimit)
            {
                // Trigger new block!
                state.BlockedUntil = now.Add(rule.BlockDuration);
                var retryAfter = (int)rule.BlockDuration.TotalSeconds;

                return new RateLimitCheckResult(
                    IsAllowed: false,
                    IsBlocked: true,
                    Limit: rule.PermitLimit,
                    Remaining: 0,
                    ResetSeconds: retryAfter,
                    RetryAfter: retryAfter,
                    BlockedUntil: state.BlockedUntil.Value
                );
            }

            // Allowed, record timestamp
            state.Timestamps.Add(now);
            var remaining = Math.Max(0, rule.PermitLimit - state.Timestamps.Count);
            var oldestTimestamp = state.Timestamps[0];
            var resetSeconds = Math.Max(1, (int)Math.Ceiling((oldestTimestamp.Add(rule.Window) - now).TotalSeconds));

            return new RateLimitCheckResult(
                IsAllowed: true,
                IsBlocked: false,
                Limit: rule.PermitLimit,
                Remaining: remaining,
                ResetSeconds: resetSeconds,
                RetryAfter: 0,
                BlockedUntil: null
            );
        }
    }
}
