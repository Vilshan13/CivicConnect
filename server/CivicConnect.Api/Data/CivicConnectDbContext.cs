using CivicConnect.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Api.Data;

public sealed class CivicConnectDbContext(DbContextOptions<CivicConnectDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<IssueCategory> Categories => Set<IssueCategory>();
    public DbSet<IssueReport> Reports => Set<IssueReport>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<PilotArea> PilotAreas => Set<PilotArea>();
    public DbSet<ReportStatusHistory> StatusHistory => Set<ReportStatusHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IssueCategory>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<IssueReport>().HasIndex(report => report.ReferenceNumber).IsUnique();
        modelBuilder.Entity<IssueReport>().Property(report => report.Description).HasMaxLength(2000);
        modelBuilder.Entity<IssueReport>().Property(report => report.Status).HasMaxLength(40);
        modelBuilder.Entity<Department>().HasIndex(department => department.Name).IsUnique();
        modelBuilder.Entity<PilotArea>().HasIndex(area => new { area.Name, area.District }).IsUnique();
        modelBuilder.Entity<IssueReport>().HasOne(report => report.Category)
            .WithMany(category => category.Reports).HasForeignKey(report => report.CategoryId);
        modelBuilder.Entity<IssueReport>().HasOne(report => report.PilotArea)
            .WithMany(area => area.Reports).HasForeignKey(report => report.PilotAreaId);
        modelBuilder.Entity<IssueReport>().HasOne(report => report.Reporter)
            .WithMany(user => user.Reports).HasForeignKey(report => report.ReporterId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueReport>().HasOne(report => report.AssignedOfficer)
            .WithMany().HasForeignKey(report => report.AssignedOfficerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<IssueReport>().HasOne(report => report.Department)
            .WithMany(department => department.Reports).HasForeignKey(report => report.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ReportStatusHistory>().HasOne(history => history.Report)
            .WithMany(report => report.StatusHistory).HasForeignKey(history => history.ReportId);
        modelBuilder.Entity<Notification>().HasOne(notification => notification.User)
            .WithMany(user => user.Notifications).HasForeignKey(notification => notification.UserId);
    }
}
