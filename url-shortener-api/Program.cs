using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using URLShortener.Data;
using URLShortener.Middleware;
using URLShortener.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSingleton<IRateLimiterService, RateLimiterService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is required");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Shortpath";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ShortpathWeb";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var corsOrigin = builder.Configuration["Cors:Origin"] ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
        policy.WithOrigins(corsOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Retry-After", "X-RateLimit-Blocked", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset"));
});

var app = builder.Build();

app.UseCors("Web");
app.UseMiddleware<RateLimitingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/", () => Results.Redirect(corsOrigin));

app.MapGet("/{shortCode}", async (string shortCode, AppDbContext db, HttpContext httpContext) =>
{
    if (string.IsNullOrWhiteSpace(shortCode))
    {
        return Results.BadRequest(new { message = "Invalid short code." });
    }

    var record = await db.Urls.FirstOrDefaultAsync(u => u.ShortCode == shortCode);
    if (record == null)
    {
        return Results.NotFound(new { message = $"Short link '{shortCode}' not found." });
    }

    record.ClickCount++;

    var clientIp = httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var fwd) && !string.IsNullOrWhiteSpace(fwd)
        ? fwd.ToString().Split(',')[0].Trim()
        : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var referrer = httpContext.Request.Headers.Referer.ToString();
    var language = httpContext.Request.Headers.AcceptLanguage.ToString();

    var click = new Clicks
    {
        UrlId = record.Id,
        Referrer = referrer,
        UserAgent = userAgent,
        IpAddress = clientIp,
        Language = language,
        Urls = record
    };

    await db.Clicks.AddAsync(click);
    await db.SaveChangesAsync();

    return Results.Redirect(record.OriginalUrl);
});

app.MapControllers();
app.Run();
