using CivicConnect.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<ReportStatusHistory> ReportStatusHistories { get; set; } = null!;

    public DbSet<Report> Reports { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}