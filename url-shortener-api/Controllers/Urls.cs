using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using URLShortener.Entities;
using URLShortener.Data;
using Microsoft.EntityFrameworkCore;
using URLShortener.utilities;
using System.Net;
using System.Text.RegularExpressions;


namespace URLShortener.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class Url(AppDbContext db) : ControllerBase
    {

        public record ShortenRequest(string LongUrl, string? CustomCode);
        [HttpPost("shorten")]
        public async Task<IActionResult> Shorten(ShortenRequest request)
        {
            if (string.IsNullOrEmpty(request.LongUrl))
            {
                return BadRequest("Bad URL");
            }

            if (!Uri.TryCreate(request.LongUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest("Bad URL");
            }
            if (request.CustomCode != null && !await db.Urls.AnyAsync(c => c.ShortCode == request.CustomCode))
            {
                string pattern = @"^[a-zA-Z0-9_-]{3,32}$";
                bool match = Regex.IsMatch(request.CustomCode, pattern);
                if (match == false)
                {
                    return Problem("Custom Code not match");
                }
            }
            else
            {
                return Conflict(new { message = $"An existing record with the id '{request.CustomCode}' was already found." });
            }

            var newUrl = new Urls
            {
                OriginalUrl = request.LongUrl,

                ShortCode = "temp"
            };

            db.Urls.Add(newUrl);
            await db.SaveChangesAsync();

            newUrl.ShortCode = request.CustomCode ?? Utilities.Encode(newUrl.Id);

            await db.SaveChangesAsync();
            Console.WriteLine($"original: {request.LongUrl}, shortCode given: {newUrl.ShortCode}");
            return Ok(new { ShortUrl = $"http://localhost:5062/{newUrl.ShortCode}" });
        }

        [HttpGet("stats/{shortCode}")]

        public async Task<IActionResult> Statistics([FromRoute] string shortCode)
        {
            var input = Utilities.Decode(shortCode);
            Clicks[] stats = await db.Clicks.Where(u => u.UrlId == input).ToArrayAsync();
            if (stats == null)
            {
                return Ok("No stats yet for this route");
            }

            return Ok(stats);
        }

        [HttpGet("{shortCode}")]
        public async Task<IActionResult> Get([FromRoute] string shortCode)
        {
            Console.WriteLine($"Parameter reached: {shortCode}");
            // Validate

            if (string.IsNullOrWhiteSpace(shortCode))
            {
                return BadRequest("bad Request.");
            }

            Urls record;
            try
            {

                record = await db.Urls
                .Where(u => u.ShortCode == shortCode).SingleAsync();
            }
            catch
            {
                return Ok(new { ShortCode = $"{shortCode}: Not found" });
            }
            record.ClickCount++;


            // Get click analytics
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            string referrer = HttpContext.Request.Headers.Referer.ToString();
            string language = HttpContext.Request.Headers.AcceptLanguage.ToString();
            Clicks analytics = new Clicks
            {
                UrlId = record.Id,
                Referrer = referrer,
                UserAgent = userAgent,
                IpAddress = clientIp,
                Urls = record
            };
            await db.Clicks.AddAsync(analytics);
            await db.SaveChangesAsync();
            return Ok(new { OriginalUrl = record.OriginalUrl });
        }


        [HttpDelete("{shortCode}")]
        public async Task<IActionResult> Delete(string shortCode, AppDbContext db)
        {
            var input = await db.Urls.Where(c => c.ShortCode == shortCode).SingleOrDefaultAsync();
            if (input == null)
            {
                return NotFound();
            }

            db.Urls.Remove(input);
            await db.SaveChangesAsync();
            return Ok(new { Message = "Data was successfully deleted" });
        }
    }
}
