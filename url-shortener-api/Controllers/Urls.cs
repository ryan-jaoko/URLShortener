using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using URLShortener.Data;
using URLShortener.utilities;

namespace URLShortener.Controllers;

[Route("api/[controller]")]
[ApiController]
public class Url(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    public record ShortenRequest(string LongUrl, string? CustomCode);

    [Authorize]
    [HttpPost("shorten")]
    public async Task<IActionResult> Shorten([FromBody] ShortenRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "You must be logged in to shorten links." });
        }

        if (string.IsNullOrWhiteSpace(request.LongUrl))
        {
            return BadRequest(new { message = "Bad URL." });
        }

        if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return BadRequest(new { message = "Bad URL. Must be a valid HTTP or HTTPS URL." });
        }

        if (!string.IsNullOrWhiteSpace(request.CustomCode))
        {
            const string pattern = @"^[a-zA-Z0-9_-]{3,32}$";
            if (!Regex.IsMatch(request.CustomCode, pattern))
            {
                return BadRequest(new { message = "Custom code must be 3-32 characters long and contain only letters, numbers, underscores, or hyphens." });
            }

            var codeExists = await db.Urls.AnyAsync(c => c.ShortCode == request.CustomCode);
            if (codeExists)
            {
                return Conflict(new { message = $"An existing record with the id '{request.CustomCode}' was already found." });
            }
        }

        var newUrl = new Urls
        {
            OriginalUrl = request.LongUrl,
            ShortCode = request.CustomCode ?? "temp",
            UserId = userId
        };

        db.Urls.Add(newUrl);
        await db.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(request.CustomCode))
        {
            newUrl.ShortCode = Utilities.Encode(newUrl.Id);
            await db.SaveChangesAsync();
        }

        var baseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/')
            ?? $"{Request.Scheme}://{Request.Host}";
        var shortUrl = $"{baseUrl}/{newUrl.ShortCode}";

        return Created($"/api/url/{newUrl.ShortCode}", new
        {
            id = newUrl.Id,
            originalUrl = newUrl.OriginalUrl,
            shortCode = newUrl.ShortCode,
            shortUrl,
            createdAt = newUrl.CreatedAt,
            clickCount = newUrl.ClickCount
        });
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { message = "Unauthorized." });
        }

        var baseUrl = configuration["PublicBaseUrl"]?.TrimEnd('/')
            ?? $"{Request.Scheme}://{Request.Host}";

        var urls = await db.Urls
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                id = u.Id,
                originalUrl = u.OriginalUrl,
                shortCode = u.ShortCode,
                shortUrl = $"{baseUrl}/{u.ShortCode}",
                createdAt = u.CreatedAt,
                clickCount = u.ClickCount
            })
            .ToListAsync();

        return Ok(urls);
    }

    [HttpGet("stats/{shortCode}")]
    public async Task<IActionResult> Statistics([FromRoute] string shortCode)
    {
        var record = await db.Urls.FirstOrDefaultAsync(u => u.ShortCode == shortCode);
        if (record == null)
        {
            return NotFound(new { message = $"Short link '{shortCode}' not found." });
        }

        // Ownership check: if owned, only owner can view stats
        if (record.UserId != null)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? callerId = Guid.TryParse(userIdValue, out var parsed) ? parsed : null;
            if (callerId != record.UserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Not your link." });
            }
        }

        var clicks = await db.Clicks
            .Where(u => u.UrlId == record.Id)
            .OrderByDescending(u => u.Timestamp)
            .ToListAsync();

        var byDay = clicks
            .GroupBy(c => c.Timestamp.UtcDateTime.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g => new { label = g.Key, count = g.Count() })
            .ToList();

        var byReferrer = clicks
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Referrer) ? "direct" : c.Referrer)
            .OrderByDescending(g => g.Count())
            .Select(g => new { label = g.Key, count = g.Count() })
            .ToList();

        return Ok(new
        {
            shortCode = record.ShortCode,
            originalUrl = record.OriginalUrl,
            clickCount = record.ClickCount,
            clicks = clicks.Select(c => new
            {
                id = c.Id,
                timestamp = c.Timestamp.ToString("o"),
                referrer = string.IsNullOrWhiteSpace(c.Referrer) ? "direct" : c.Referrer,
                userAgent = c.UserAgent,
                ipAddress = c.IpAddress,
                language = c.Language
            }),
            byDay,
            byReferrer
        });
    }

    [HttpDelete("{shortCode}")]
    public async Task<IActionResult> Delete([FromRoute] string shortCode)
    {
        var record = await db.Urls.FirstOrDefaultAsync(c => c.ShortCode == shortCode);
        if (record == null)
        {
            return NotFound(new { message = "Link not found." });
        }

        if (record.UserId != null)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? callerId = Guid.TryParse(userIdValue, out var parsed) ? parsed : null;
            if (callerId != record.UserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Not your link." });
            }
        }

        db.Urls.Remove(record);
        await db.SaveChangesAsync();
        return Ok(new { message = "Data was successfully deleted" });
    }
}
