using CivicConnect.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Api.Data;

public sealed class CivicConnectDbContext(DbContextOptions<CivicConnectDbContext> options) : DbContext(options)
{
    public DbSet<IssueCategory> Categories => Set<IssueCategory>();
    public DbSet<IssueReport> Reports => Set<IssueReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IssueCategory>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<IssueReport>().HasIndex(report => report.ReferenceNumber).IsUnique();
        modelBuilder.Entity<IssueReport>().Property(report => report.Description).HasMaxLength(2000);
        modelBuilder.Entity<IssueReport>().Property(report => report.Status).HasMaxLength(40);
        modelBuilder.Entity<IssueReport>().HasOne(report => report.Category)
            .WithMany(category => category.Reports).HasForeignKey(report => report.CategoryId);
    }
}
