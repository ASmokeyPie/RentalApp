using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RentalApp.Database.Models;

namespace RentalApp.Database.Data;

public class AppDbContext : DbContext
{

    public AppDbContext()
    { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;

        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            var a = Assembly.GetExecutingAssembly();
            using var stream = a.GetManifestResourceStream("RentalApp.Database.appsettings.json");

            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            connectionString = config.GetConnectionString("DevelopmentConnection");
        }

        optionsBuilder.UseNpgsql(connectionString, o => o.UseNetTopologySuite());
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Review> Reviews { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();
        });

        // Item
        modelBuilder.Entity<Item>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(100);
            entity.Property(e => e.DailyRate).HasColumnType("numeric(10,2)");
            entity.Property(e => e.Location).HasColumnType("geography (Point, 4326)");

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Items)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.ItemsOwned)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Rental
        modelBuilder.Entity<Rental>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.TotalPrice).HasColumnType("numeric(10,2)");

            entity.HasOne(e => e.Item)
                  .WithMany(i => i.Rentals)
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Borrower)
                  .WithMany(u => u.RentalsAsBorrower)
                  .HasForeignKey(e => e.BorrowerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Review
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => e.RentalId).IsUnique();   // one review per rental
            entity.Property(e => e.Comment).HasMaxLength(500);

            entity.HasOne(e => e.Rental)
                  .WithOne(r => r.Review)
                  .HasForeignKey<Review>(e => e.RentalId)
                  .OnDelete(DeleteBehavior.Cascade);       // delete rental → delete its review

            entity.HasOne(e => e.Reviewer)
                  .WithMany(u => u.ReviewsWritten)
                  .HasForeignKey(e => e.ReviewerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasData(
                new Category { Id = 1, Name = "Power Tools", Slug = "power-tools" },
                new Category { Id = 2, Name = "Hand Tools", Slug = "hand-tools" },
                new Category { Id = 3, Name = "Camping Gear", Slug = "camping-gear" },
                new Category { Id = 4, Name = "Electronics", Slug = "electronics" },
                new Category { Id = 5, Name = "Photography", Slug = "photography" },
                new Category { Id = 6, Name = "Kitchen Appliances", Slug = "kitchen-appliances" },
                new Category { Id = 7, Name = "Sports Equipment", Slug = "sports-equipment" },
                new Category { Id = 8, Name = "Musical Instruments", Slug = "musical-instruments" },
                new Category { Id = 9, Name = "Party & Events", Slug = "party-and-events" },
                new Category { Id = 10, Name = "Garden & Outdoor", Slug = "garden-and-outdoor" },
                new Category { Id = 11, Name = "Bikes & Scooters", Slug = "bikes-and-scooters" },
                new Category { Id = 12, Name = "Books & Media", Slug = "books-and-media" }
            );
        });
    }

}