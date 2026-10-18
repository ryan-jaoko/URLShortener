using System.Text.Json;
using URLShortener.Services;

namespace URLShortener.Middleware;

public class RateLimitingMiddleware(RequestDelegate next, IRateLimiterService rateLimiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only enforce on API endpoints
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var clientIp = GetClientIp(context);

        var category = "general";
        if (path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            category = "auth";
        }
        else if (path.StartsWith("/api/url/shorten", StringComparison.OrdinalIgnoreCase) &&
                 HttpMethods.IsPost(context.Request.Method))
        {
            category = "shorten";
        }

        var result = rateLimiter.CheckRateLimit(clientIp, category);

        if (result.IsBlocked || !result.IsAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = result.RetryAfter.ToString();
            context.Response.Headers["X-RateLimit-Blocked"] = "true";
            context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = "0";
            context.Response.Headers["X-RateLimit-Reset"] = result.ResetSeconds.ToString();

            var responseBody = new
            {
                statusCode = 429,
                error = "Too Many Requests",
                message = $"Rate limit exceeded. You are temporarily blocked. Please wait {result.RetryAfter} seconds.",
                retryAfter = result.RetryAfter,
                blockedUntil = result.BlockedUntil
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(responseBody));
            return;
        }

        // Apply rate limit response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
            context.Response.Headers["X-RateLimit-Reset"] = result.ResetSeconds.ToString();
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) && !string.IsNullOrWhiteSpace(forwarded))
        {
            var firstIp = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(firstIp))
            {
                return firstIp;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }
}
