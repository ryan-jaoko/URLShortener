using URLShortener.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using WorkOS;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text.Encodings.Web;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];

// Configure WorkOS client
var workosApiKey = builder.Configuration["WorkOS:ApiKey"];
var workosClientId = builder.Configuration["WorkOS:ClientId"];
var workosRedirectUri = builder.Configuration["WorkOS:RedirectUri"]
    ?? throw new InvalidOperationException("WorkOS:RedirectUri configuration is required");

var workosClient = new WorkOSClient(new WorkOSOptions
{
    ApiKey = workosApiKey,
    ClientId = workosClientId
});
WorkOSConfiguration.WorkOSClient = workosClient;

builder.Services.AddSingleton(workosClient);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add session support for authentication
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});


builder.Services.AddAuthorization();



var corsOrigin = builder.Configuration["Cors:Origin"] ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
        policy.WithOrigins(corsOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("Web");


// Enable session middleware
app.UseSession();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGet("/{shortCode}", async (string shortCode, AppDbContext db, HttpContext HttpContext) => {
    Console.WriteLine($"Parameter reached: {shortCode}");
    // Validate

    if (string.IsNullOrWhiteSpace(shortCode))
    {
        return Results.BadRequest("bad Request.");
    }

    Urls record;
    try
    {
        record = await db.Urls
        .Where(u => u.ShortCode == shortCode).SingleAsync();
    }
    catch
    {
        return Results.Ok(new { ShortCode = $"{shortCode}: Not found" });
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
    return Results.Redirect(record.OriginalUrl);
});

// Auth endpoints
app.MapGet("/auth/login", (WorkOSClient client) =>
{
    var authorizationUrl = client.UserManagement.GetAuthorizationUrl(
        new UserManagementGetAuthorizationUrlOptions
        {
            RedirectUri = workosRedirectUri,
            Provider = UserManagementAuthenticationProvider.Authkit
        });

    return Results.Redirect(authorizationUrl);
});

app.MapGet("/auth/callback", async (HttpContext context, WorkOSClient client) =>
{
    var code = context.Request.Query["code"].ToString();

    if (string.IsNullOrEmpty(code))
    {
        return Results.BadRequest("Authorization code not provided");
    }

    try
    {
        var authResponse = await client.UserManagement.AuthenticateWithCodeAsync(
            new AuthenticateWithCodeOptions
            {
                Code = code
            });

        // Store user info in session
        context.Session.SetString("user_id", authResponse.User.Id ?? "");
        context.Session.SetString("user_email", authResponse.User.Email ?? "");
        context.Session.SetString("user_name",
            $"{authResponse.User.FirstName} {authResponse.User.LastName}".Trim());

        return Results.Redirect("/");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Authentication failed: {ex.Message}");
    }
});

app.MapGet("/auth/logout", (HttpContext context) =>
{
    context.Session.Clear();
    return Results.Redirect(corsOrigin);
});

app.MapGet("/auth/me", (HttpContext context) =>
{
    var userId = context.Session.GetString("user_id");

    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        id = userId,
        email = context.Session.GetString("user_email"),
        name = context.Session.GetString("user_name")
    });
});


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
