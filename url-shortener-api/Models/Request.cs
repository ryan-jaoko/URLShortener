namespace URLShortener.Models;

public class RegisterRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class UserDto
{
    public required string Username { get; set; } = string.Empty;
    public required string Email { get; set; } = string.Empty;
    public required string Password { get; set; } = string.Empty;
}

public class UserResponseDto
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
}

public class AuthResponseDto
{
    public required string Token { get; set; }
    public required UserResponseDto User { get; set; }
}