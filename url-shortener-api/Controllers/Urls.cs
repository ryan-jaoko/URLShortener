using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using URLShortener.Data;
using Microsoft.EntityFrameworkCore;
using URLShortener.utilities;
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
            if (!string.IsNullOrWhiteSpace(request.CustomCode) && !await db.Urls.AnyAsync(c => c.ShortCode == request.CustomCode))
            {
                string pattern = @"^[a-zA-Z0-9_-]{3,32}$";
                bool match = Regex.IsMatch(request.CustomCode, pattern);
                if (match == false)
                {
                    return Problem("Invalid custom code format");
                }
            }
            else
            {
                if(string.IsNullOrEmpty(request.CustomCode) || request.CustomCode == "" )
                {
                    //nothing
                }
                else
                {
                    return Conflict(new { message = $"An existing record with the id '{request.CustomCode}' was already found." });
                }
            }

            var newUrl = new Urls
            {
                OriginalUrl = request.LongUrl,

                ShortCode = request.CustomCode ?? "temp"
            };

            db.Urls.Add(newUrl);
            await db.SaveChangesAsync();
            if (request.CustomCode == null)
            {
                newUrl.ShortCode = Utilities.Encode(newUrl.Id);
                await db.SaveChangesAsync();
            }
            Console.WriteLine($"original: {request.LongUrl}, shortCode given: {newUrl.ShortCode}");
            return Created($"/api/url/{newUrl.ShortCode}", new
            {
                id = newUrl.Id,
                originalUrl = newUrl.OriginalUrl,
                shortCode = newUrl.ShortCode,
                shortUrl = $"{Request.Host}/{newUrl.ShortCode}",
                createdAt = newUrl.CreatedAt,
                clickCount = newUrl.ClickCount
            });
        }

        [HttpGet("stats/{shortCode}")]

        public async Task<IActionResult> Statistics([FromRoute] string shortCode)
        {
            var input = Utilities.Decode(shortCode);
            var stats = await db.Clicks.Where(u => u.UrlId == input).ToArrayAsync();
            if (stats == null)
            {
                return Ok("No stats yet for this route");
            }

            return Ok(stats);
        }

        // Comment Unsuded Code.
        // [HttpGet("")]
        // public async Task<IActionResult> Get([FromRoute] string shortCode)
        // {

        // }


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
