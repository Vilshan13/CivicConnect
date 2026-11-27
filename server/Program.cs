using CivicConnect.Api.Models;
using CivicConnect.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var webRootPath = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads"));

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
app.UseStaticFiles();

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
    report.PhotoPath = null;
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

app.MapPost("/api/reports/with-photo", async Task<IResult> (
    [FromForm] CreateReportWithPhotoRequest request,
    ClaimsPrincipal user,
    AppDbContext db,
    IWebHostEnvironment environment) =>
{
    var citizenId = user.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrWhiteSpace(citizenId))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 120 ||
        string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 2000 ||
        string.IsNullOrWhiteSpace(request.Category) || request.Category.Length > 80 ||
        string.IsNullOrWhiteSpace(request.AreaName) || request.AreaName.Length > 100 ||
        !double.IsFinite(request.Latitude) || request.Latitude is < -90 or > 90 ||
        !double.IsFinite(request.Longitude) || request.Longitude is < -180 or > 180)
    {
        return Results.BadRequest(new { message = "Check the report details and location." });
    }

    var photo = request.Photo;
    if (photo is null || photo.Length == 0)
    {
        return Results.BadRequest(new { message = "Choose a photo to upload." });
    }

    const long maxPhotoSizeBytes = 5 * 1024 * 1024;
    if (photo.Length > maxPhotoSizeBytes)
    {
        return Results.BadRequest(new { message = "The image must be 5 MB or smaller." });
    }

    var contentType = photo.ContentType.ToLowerInvariant();
    var extension = contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => null
    };

    if (extension is null)
    {
        return Results.BadRequest(new { message = "Choose a JPG, PNG, or WebP image." });
    }

    var header = new byte[12];
    int headerLength;
    await using (var input = photo.OpenReadStream())
    {
        headerLength = await input.ReadAsync(header);
    }

    var validSignature = contentType switch
    {
        "image/jpeg" => headerLength >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
        "image/png" => headerLength >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E &&
            header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A &&
            header[6] == 0x1A && header[7] == 0x0A,
        "image/webp" => headerLength >= 12 &&
            header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F' &&
            header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P',
        _ => false
    };

    if (!validSignature)
    {
        return Results.BadRequest(new { message = "That file does not appear to be a valid image." });
    }

    var uploadsDirectory = Path.Combine(
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"),
        "uploads");
    Directory.CreateDirectory(uploadsDirectory);

    var fileName = $"{Guid.NewGuid():N}{extension}";
    var filePath = Path.Combine(uploadsDirectory, fileName);
    await using (var output = File.Create(filePath))
    {
        await photo.CopyToAsync(output);
    }

    var report = new Report
    {
        CitizenId = citizenId,
        Title = request.Title.Trim(),
        Description = request.Description.Trim(),
        Category = request.Category.Trim(),
        AreaName = request.AreaName.Trim(),
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        PhotoPath = $"/uploads/{fileName}",
        Status = "Submitted",
        CreatedAtUtc = DateTime.UtcNow
    };

    report.StatusHistory.Add(new ReportStatusHistory
    {
        PreviousStatus = null,
        NewStatus = report.Status,
        Note = "Report submitted"
    });

    db.Reports.Add(report);
    try
    {
        await db.SaveChangesAsync();
    }
    catch
    {
        File.Delete(filePath);
        throw;
    }

    return Results.Created($"/api/reports/{report.Id}", report);
})
.RequireAuthorization(policy => policy.RequireRole("Citizen"))
.WithMetadata(new RequestSizeLimitAttribute(6 * 1024 * 1024))
.DisableAntiforgery();

app.Run();


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
