using CivicConnect.Api.Models;
using CivicConnect.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/api/reports", async (AppDbContext db) =>
    await db.Reports
        .OrderByDescending(report => report.CreatedAtUtc)
        .ToListAsync());

        app.MapGet("/api/reports/{id:int}", async Task<IResult> (int id, AppDbContext db) =>
{
    var report = await db.Reports
        .Include(report => report.StatusHistory)
        .FirstOrDefaultAsync(report => report.Id == id);

    if (report is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(report);
});
app.MapPatch("/api/reports/{id:int}/status",
    async Task<IResult> (int id, UpdateReportStatusRequest request, AppDbContext db) =>
{
    var report = await db.Reports.FindAsync(id);

    if (report is null)
    {
        return Results.NotFound();
    }

    var allowedStatuses = new[]
    {
        "Under Review",
        "Assigned",
        "In Progress",
        "Resolved"
    };

    if (!allowedStatuses.Contains(request.Status))
    {
        return Results.BadRequest(new
        {
            message = "Status must be Under Review, Assigned, In Progress, or Resolved."
        });
    }

    if (request.Note?.Length > 500)
    {
        return Results.BadRequest(new { message = "Note must be 500 characters or fewer." });
    }

    var previousStatus = report.Status;
    report.Status = request.Status;

    db.ReportStatusHistories.Add(new ReportStatusHistory
    {
        ReportId = report.Id,
        PreviousStatus = previousStatus,
        NewStatus = report.Status,
        Note = request.Note,
        ChangedAtUtc = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    return Results.Ok(report);
});
app.MapPost("/api/reports", async (Report report, AppDbContext db) =>
{
    report.Id = 0;
    report.CreatedAtUtc = DateTime.UtcNow;
    report.Status = "Submitted";

    report.StatusHistory.Add(new ReportStatusHistory
{
    PreviousStatus = null,
    NewStatus = report.Status,
    Note = "Report submitted"
});

    db.Reports.Add(report);
    await db.SaveChangesAsync();

    return Results.Created($"/api/reports/{report.Id}", report);
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
