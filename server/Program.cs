using CivicConnect.Api.Models;
using CivicConnect.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthorization();

builder.Services
    .AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

var app = builder.Build();

app.MapGroup("/api/auth").MapIdentityApi<ApplicationUser>();

app.MapPost("/api/auth/citizen-register",
    async Task<IResult> (
        RegisterCitizenRequest request,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new
        {
            message = "Email and password are required."
        });
    }

    const string citizenRole = "Citizen";

    if (!await roleManager.RoleExistsAsync(citizenRole))
    {
        var roleResult = await roleManager.CreateAsync(
            new IdentityRole(citizenRole));

        if (!roleResult.Succeeded)
        {
            return Results.BadRequest(roleResult.Errors);
        }
    }

    var email = request.Email.Trim();
    var user = new ApplicationUser
    {
        UserName = email,
        Email = email
    };

    var createResult = await userManager.CreateAsync(user, request.Password);
    if (!createResult.Succeeded)
    {
        return Results.BadRequest(createResult.Errors);
    }

    var roleAssignment = await userManager.AddToRoleAsync(user, citizenRole);
    if (!roleAssignment.Succeeded)
    {
        await userManager.DeleteAsync(user);
        return Results.Problem(detail: "Could not assign the Citizen role.");
    }

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        role = citizenRole
    });
});

app.MapGet("/api/reports", async Task<IResult> (
    ClaimsPrincipal user,
    AppDbContext db) =>
{
    var citizenId = user.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(citizenId))
    {
        return Results.Unauthorized();
    }

    var reports = await db.Reports
        .Where(report => report.CitizenId == citizenId)
        .OrderByDescending(report => report.CreatedAtUtc)
        .ToListAsync();

    return Results.Ok(reports);
})
.RequireAuthorization(policy => policy.RequireRole("Citizen"));

app.MapGet("/api/reports/{id:int}", async Task<IResult> (
    int id,
    ClaimsPrincipal user,
    AppDbContext db) =>
{
    var citizenId = user.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(citizenId))
    {
        return Results.Unauthorized();
    }

    var report = await db.Reports
        .Include(report => report.StatusHistory)
        .FirstOrDefaultAsync(report =>
            report.Id == id && report.CitizenId == citizenId);

    if (report is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(report);
})
.RequireAuthorization(policy => policy.RequireRole("Citizen"));

app.MapPatch("/api/reports/{id:int}/status",
    async Task<IResult> (
        int id,
        UpdateReportStatusRequest request,
        AppDbContext db) =>
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
        return Results.BadRequest(new
        {
            message = "Note must be 500 characters or fewer."
        });
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
})
.RequireAuthorization(policy =>
    policy.RequireRole("Officer", "Administrator"));

app.MapPost("/api/reports", async Task<IResult> (
    Report report,
    ClaimsPrincipal user,
    AppDbContext db) =>
{
    var citizenId = user.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(citizenId))
    {
        return Results.Unauthorized();
    }

    report.Id = 0;
    report.CitizenId = citizenId;
    report.CreatedAtUtc = DateTime.UtcNow;
    report.Status = "Submitted";
    report.StatusHistory.Clear();

    report.StatusHistory.Add(new ReportStatusHistory
    {
        PreviousStatus = null,
        NewStatus = report.Status,
        Note = "Report submitted"
    });

    db.Reports.Add(report);
    await db.SaveChangesAsync();

    return Results.Created($"/api/reports/{report.Id}", report);
})
.RequireAuthorization(policy => policy.RequireRole("Citizen"));

app.Run();


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
