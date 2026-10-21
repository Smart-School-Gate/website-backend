using Microsoft.EntityFrameworkCore;
using SmartSchool.Website.Api.Data.Entities;

namespace SmartSchool.Website.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.ToTable("ContactSubmissions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .ValueGeneratedNever();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(150)
                .HasColumnType("varchar(150)");

            entity.Property(e => e.SchoolName)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("varchar(200)");

            entity.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(30)
                .HasColumnType("varchar(30)");

            entity.Property(e => e.WorkEmail)
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnType("varchar(254)");

            entity.Property(e => e.Message)
                .IsRequired(false)
                .HasColumnType("text");

            entity.Property(e => e.CreatedAtUtc)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.HasIndex(e => e.CreatedAtUtc)
                .HasDatabaseName("IX_ContactSubmissions_CreatedAtUtc");
        });
    }
}
