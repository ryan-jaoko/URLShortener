using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using URLShortener.Data;
using URLShortener.Models;
using URLShortener.Services;

namespace URLShortener.Controllers;

[Route("api/[controller]")]
[ApiController]
public class Auth(AppDbContext db, ITokenService tokenService) : ControllerBase
{
    private static readonly PasswordHasher<Users> PasswordHasher = new();

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username, email, and password are required." });
        }

        var trimmedUsername = request.Username.Trim();
        var trimmedEmail = request.Email.Trim().ToLowerInvariant();

        if (trimmedUsername.Length < 3 || trimmedUsername.Length > 50)
        {
            return BadRequest(new { message = "Username must be between 3 and 50 characters." });
        }

        var emailValidator = new EmailAddressAttribute();
        if (!emailValidator.IsValid(trimmedEmail))
        {
            return BadRequest(new { message = "Invalid email format." });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password must be at least 6 characters long." });
        }

        var emailExists = await db.Users.AnyAsync(u => u.Email.ToLower() == trimmedEmail);
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        var usernameExists = await db.Users.AnyAsync(u => u.Username.ToLower() == trimmedUsername.ToLower());
        if (usernameExists)
        {
            return Conflict(new { message = "Username is already taken." });
        }

        var user = new Users
        {
            Id = Guid.CreateVersion7(),
            Username = trimmedUsername,
            Email = trimmedEmail,
            PasswordHash = string.Empty
        };

        user.PasswordHash = PasswordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id.ToString(),
                username = user.Username,
                email = user.Email
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required." });
        }

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var verificationResult = PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            token,
            user = new
            {
                id = user.Id.ToString(),
                username = user.Username,
                email = user.Email
            }
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid user token." });
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found." });
        }

        return Ok(new
        {
            id = user.Id.ToString(),
            username = user.Username,
            email = user.Email
        });
    }
}
