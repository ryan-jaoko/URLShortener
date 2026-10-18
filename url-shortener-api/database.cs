using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace URLShortener.Data;

public class AppDbContext : DbContext
{
    public DbSet<Urls> Urls { get; set; }
    public DbSet<Clicks> Clicks { get; set; }
    public DbSet<Users> Users { get; set; }

    // The constructor accepts options from Program.cs
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Urls>()
            .Property(c => c.Id)
            .UseIdentityAlwaysColumn();

        modelBuilder.Entity<Urls>()
            .HasIndex(u => u.ShortCode)
            .IsUnique();

        modelBuilder.Entity<Clicks>()
            .Property(c => c.Id)
            .UseIdentityAlwaysColumn();

        modelBuilder.Entity<Urls>()
            .Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone");

        modelBuilder.Entity<Clicks>()
        .Property(e => e.Timestamp)
        .HasColumnType("timestamp with time zone");
    }

}

public class Urls
{
    public int Id { get; set; }
    public required string OriginalUrl { get; set; }
    public required string ShortCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int ClickCount { get; set; }

    public ICollection<Clicks> Clicks { get; set; } = new List<Clicks>();


    // To be implemented when implementing User accounts
    public Guid? UserId { get; set; }
}


public class Clicks
{
    public int Id { get; set; }
    public required int UrlId { get; set; }
    [ForeignKey(nameof(UrlId))]
    public Urls? Urls { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public required string Referrer { get; set; } = "";
    public string? Language { get; set; }
}

// Rolling out auth
public class Users
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
}
