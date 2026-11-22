using CivicConnect.Api.Data;
using CivicConnect.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CivicConnectDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CivicConnect") ??
        "Host=localhost;Port=5432;Database=civicconnect;Username=civicconnect;Password=civicconnect_dev"));
builder.Services.AddCors(options => options.AddPolicy("local-web", policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("local-web");
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Categories.AnyAsync())
    {
        db.Categories.AddRange(
            new IssueCategory { Name = "Road damage", Description = "Potholes and damaged road surfaces" },
            new IssueCategory { Name = "Waste", Description = "Improper waste disposal or collection" },
            new IssueCategory { Name = "Blocked drain", Description = "Blocked or overflowing drains" },
            new IssueCategory { Name = "Streetlight", Description = "Broken or unlit streetlights" },
            new IssueCategory { Name = "Other", Description = "Another community issue" });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/categories", async (CivicConnectDbContext db) =>
    Results.Ok(await db.Categories.OrderBy(category => category.Name)
        .Select(category => new { category.Id, category.Name, category.Description }).ToListAsync()));

app.MapGet("/api/reports", async (CivicConnectDbContext db) =>
    Results.Ok(await db.Reports.OrderByDescending(report => report.CreatedAt)
        .Select(report => new
        {
            report.Id,
            report.ReferenceNumber,
            report.CategoryId,
            Category = report.Category.Name,
            report.Description,
            report.Latitude,
            report.Longitude,
            report.PhotoUrl,
            report.Status,
            report.CreatedAt
        }).ToListAsync()));

app.MapPost("/api/reports", async (HttpRequest request, CivicConnectDbContext db, IWebHostEnvironment environment) =>
{
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["categoryId"], out var categoryId) ||
        string.IsNullOrWhiteSpace(form["description"]) || form["description"].ToString().Length > 2000 ||
        !double.TryParse(form["latitude"], out var latitude) || latitude is < -90 or > 90 ||
        !double.TryParse(form["longitude"], out var longitude) || longitude is < -180 or > 180)
    {
        return Results.BadRequest(new { message = "Choose a category, add a description (up to 2,000 characters), and select a valid map location." });
    }
    if (!await db.Categories.AnyAsync(category => category.Id == categoryId))
        return Results.BadRequest(new { message = "That issue category is not available." });

    string? photoUrl = null;
    var photo = form.Files.GetFile("photo");
    if (photo is { Length: > 0 })
    {
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (photo.Length > 5 * 1024 * 1024 || !allowedTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase) ||
            !await HasMatchingImageSignatureAsync(photo))
            return Results.BadRequest(new { message = "Photo must be a JPG, PNG, or WebP image up to 5 MB." });

        var extension = photo.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => ".webp"
        };
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var uploadDirectory = Path.Combine(environment.WebRootPath ?? "wwwroot", "uploads");
        Directory.CreateDirectory(uploadDirectory);
        await using var stream = File.Create(Path.Combine(uploadDirectory, fileName));
        await photo.CopyToAsync(stream);
        photoUrl = $"/uploads/{fileName}";
    }

    var report = new IssueReport
    {
        ReferenceNumber = $"CC-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
        CategoryId = categoryId,
        Description = form["description"].ToString().Trim(),
        Latitude = latitude,
        Longitude = longitude,
        PhotoUrl = photoUrl,
        Status = "Submitted",
        CreatedAt = DateTime.UtcNow
    };
    db.Reports.Add(report);
    await db.SaveChangesAsync();
    return Results.Created($"/api/reports/{report.Id}", new
    {
        report.Id,
        report.ReferenceNumber,
        report.Status,
        report.CreatedAt
    });
});

app.Run();

static async Task<bool> HasMatchingImageSignatureAsync(IFormFile file)
{
    var header = new byte[12];
    await using var stream = file.OpenReadStream();
    var read = await stream.ReadAsync(header);
    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    if (file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        return read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    return read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
        header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
}
