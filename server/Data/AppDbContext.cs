using Microsoft.EntityFrameworkCore;
using CivicConnect.Api.Models;

namespace CivicConnect.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<ReportStatusHistory> ReportStatusHistories { get; set; } = null!;

    public DbSet<Report> Reports { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
}