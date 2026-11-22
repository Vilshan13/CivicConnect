using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using CivicConnect.Api.Data;
using CivicConnect.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;

const string CitizenRole = "Citizen";
const string OfficerRole = "Officer";
const string AdminRole = "Administrator";
const string Issuer = "CivicConnect";

var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey) && builder.Environment.IsDevelopment())
    signingKey = "civicconnect-local-only-signing-key-change-before-deploying";
if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
    throw new InvalidOperationException("Set Jwt:SigningKey to a secret of at least 32 bytes before starting the API.");

builder.Services.AddDbContext<CivicConnectDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CivicConnect") ??
        (builder.Environment.IsDevelopment()
            ? "Host=localhost;Port=5432;Database=civicconnect;Username=civicconnect;Password=civicconnect_dev"
            : throw new InvalidOperationException("Set ConnectionStrings:CivicConnect before starting outside Development."))));
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<CivicConnectDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(options => options.AddPolicy("local-web", policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod()));
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 6 * 1024 * 1024);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseCors("local-web");
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data", "uploads"));
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CivicConnectDbContext>();
    await db.Database.MigrateAsync();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { CitizenRole, OfficerRole, AdminRole })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

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

    var adminEmail = builder.Configuration["SeedAdmin:Email"];
    var adminPassword = builder.Configuration["SeedAdmin:Password"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var initialAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (initialAdmin is null)
        {
            initialAdmin = new AppUser { UserName = adminEmail, Email = adminEmail, DisplayName = "CivicConnect Administrator", EmailConfirmed = true };
            var result = await userManager.CreateAsync(initialAdmin, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create the configured administrator: {string.Join(", ", result.Errors.Select(error => error.Description))}");
        }
        if (!await userManager.IsInRoleAsync(initialAdmin, AdminRole))
            await userManager.AddToRoleAsync(initialAdmin, AdminRole);
    }
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/auth/register", async (RegisterRequest request, UserManager<AppUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 100 ||
        string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254)
        return Results.BadRequest(new { message = "Enter a name and a valid email address." });

    var user = new AppUser
    {
        UserName = request.Email.Trim(),
        Email = request.Email.Trim(),
        DisplayName = request.DisplayName.Trim()
    };
    var created = await users.CreateAsync(user, request.Password ?? string.Empty);
    if (!created.Succeeded)
        return Results.BadRequest(new { message = string.Join(" ", created.Errors.Select(error => error.Description)) });
    await users.AddToRoleAsync(user, CitizenRole);
    var roles = await users.GetRolesAsync(user);
    return Results.Ok(CreateToken(user, roles, signingKey));
});

app.MapPost("/api/auth/login", async (LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
{
    var user = await users.FindByEmailAsync(request.Email ?? string.Empty);
    if (user is null) return Results.Json(new { message = "Email or password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
    var passwordCheck = await signIn.CheckPasswordSignInAsync(user, request.Password ?? string.Empty, lockoutOnFailure: true);
    if (!passwordCheck.Succeeded)
        return Results.Json(new { message = "Email or password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
    var roles = await users.GetRolesAsync(user);
    return Results.Ok(CreateToken(user, roles, signingKey));
});

app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<AppUser> users) =>
{
    var user = await users.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    return Results.Ok(new { user.Id, user.DisplayName, user.Email, Roles = await users.GetRolesAsync(user), user.DepartmentId });
}).RequireAuthorization();

app.MapGet("/api/categories", async (CivicConnectDbContext db) =>
    Results.Ok(await db.Categories.OrderBy(category => category.Name)
        .Select(category => new { category.Id, category.Name, category.Description }).ToListAsync()));
app.MapGet("/api/areas", async (CivicConnectDbContext db) =>
    Results.Ok(await db.PilotAreas.OrderBy(area => area.District).ThenBy(area => area.Name)
        .Select(area => new { area.Id, area.Name, area.District }).ToListAsync()));

app.MapGet("/api/reports", async (ClaimsPrincipal user, CivicConnectDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var isAdmin = user.IsInRole(AdminRole);
    var isOfficer = user.IsInRole(OfficerRole);
    var departmentId = int.TryParse(user.FindFirstValue("department_id"), out var parsedDepartment) ? parsedDepartment : (int?)null;
    var query = db.Reports.AsNoTracking().AsQueryable();
    if (!isAdmin && !isOfficer)
        query = query.Where(report => report.ReporterId == userId);
    else if (isOfficer && !isAdmin)
        query = query.Where(report => report.AssignedOfficerId == userId ||
            (report.AssignedOfficerId == null && departmentId != null &&
             (report.DepartmentId == null || report.DepartmentId == departmentId)));

    var reports = await query.OrderByDescending(report => report.CreatedAt)
        .Select(report => new
        {
            report.Id,
            report.ReferenceNumber,
            report.CategoryId,
            Category = report.Category.Name,
            report.PilotAreaId,
            Area = report.PilotArea.Name,
            District = report.PilotArea.District,
            report.Description,
            report.Latitude,
            report.Longitude,
            HasPhoto = report.PhotoFileName != null,
            report.Status,
            report.CreatedAt,
            report.UpdatedAt,
            report.DepartmentId,
            Department = report.Department == null ? null : report.Department.Name,
            report.AssignedOfficerId,
            AssignedOfficer = report.AssignedOfficer == null ? null : report.AssignedOfficer.DisplayName,
            StatusHistory = report.StatusHistory.OrderByDescending(history => history.CreatedAt)
                .Select(history => new { history.FromStatus, history.ToStatus, history.Note, history.CreatedAt }).ToList()
        }).ToListAsync();
    return Results.Ok(reports);
}).RequireAuthorization();

app.MapGet("/api/reports/{id:int}", async (int id, ClaimsPrincipal user, CivicConnectDbContext db) =>
{
    var report = await db.Reports.AsNoTracking().Include(item => item.StatusHistory).Include(item => item.PilotArea).Include(item => item.Department)
        .Include(item => item.AssignedOfficer).Include(item => item.Category).FirstOrDefaultAsync(item => item.Id == id);
    if (report is null) return Results.NotFound();
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    if (report.ReporterId != userId && !user.IsInRole(OfficerRole) && !user.IsInRole(AdminRole))
        return Results.Forbid();
    if (user.IsInRole(OfficerRole) && !user.IsInRole(AdminRole) &&
        report.AssignedOfficerId != userId && report.DepartmentId != null &&
        report.DepartmentId.ToString() != user.FindFirstValue("department_id"))
        return Results.Forbid();
    return Results.Ok(new
    {
        report.Id, report.ReferenceNumber, report.CategoryId, Category = report.Category.Name,
        report.PilotAreaId, Area = report.PilotArea.Name, District = report.PilotArea.District,
        report.Description, report.Latitude, report.Longitude, HasPhoto = report.PhotoFileName != null, report.Status,
        report.CreatedAt, report.UpdatedAt, report.DepartmentId, Department = report.Department?.Name,
        report.AssignedOfficerId, AssignedOfficer = report.AssignedOfficer?.DisplayName,
        StatusHistory = report.StatusHistory.OrderByDescending(history => history.CreatedAt)
            .Select(history => new { history.FromStatus, history.ToStatus, history.Note, history.CreatedAt })
    });
}).RequireAuthorization();

app.MapGet("/api/reports/{id:int}/photo", async (int id, ClaimsPrincipal user, CivicConnectDbContext db, IWebHostEnvironment environment) =>
{
    var report = await db.Reports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
    if (report is null || string.IsNullOrWhiteSpace(report.PhotoFileName)) return Results.NotFound();
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    if (report.ReporterId != userId && !user.IsInRole(OfficerRole) && !user.IsInRole(AdminRole))
        return Results.Forbid();
    if (user.IsInRole(OfficerRole) && !user.IsInRole(AdminRole) &&
        report.AssignedOfficerId != userId && report.DepartmentId != null &&
        report.DepartmentId.ToString() != user.FindFirstValue("department_id"))
        return Results.Forbid();

    var fileName = Path.GetFileName(report.PhotoFileName);
    var filePath = Path.Combine(environment.ContentRootPath, "App_Data", "uploads", fileName);
    if (!File.Exists(filePath)) return Results.NotFound();
    var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".jpg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
    return Results.File(filePath, contentType);
}).RequireAuthorization();

app.MapPost("/api/reports", async (HttpRequest request, ClaimsPrincipal user, CivicConnectDbContext db, IWebHostEnvironment environment) =>
{
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["categoryId"], out var categoryId) ||
        !int.TryParse(form["pilotAreaId"], out var pilotAreaId) ||
        string.IsNullOrWhiteSpace(form["description"]) || form["description"].ToString().Length > 2000 ||
        !double.TryParse(form["latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) || latitude is < -90 or > 90 ||
        !double.TryParse(form["longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) || longitude is < -180 or > 180)
        return Results.BadRequest(new { message = "Choose a category, add a description (up to 2,000 characters), and select a valid map location." });
    if (!await db.Categories.AnyAsync(category => category.Id == categoryId))
        return Results.BadRequest(new { message = "That issue category is not available." });
    if (!await db.PilotAreas.AnyAsync(area => area.Id == pilotAreaId))
        return Results.BadRequest(new { message = "Choose an available pilot area." });

    string? photoFileName = null;
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
        var uploadDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(uploadDirectory);
        await using var stream = File.Create(Path.Combine(uploadDirectory, fileName));
        await photo.CopyToAsync(stream);
        photoFileName = fileName;
    }

    var reporterId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var report = new IssueReport
    {
        ReferenceNumber = $"CC-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
        CategoryId = categoryId,
        PilotAreaId = pilotAreaId,
        Description = form["description"].ToString().Trim(),
        Latitude = latitude,
        Longitude = longitude,
        PhotoFileName = photoFileName,
        Status = "Submitted",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        ReporterId = reporterId
    };
    report.StatusHistory.Add(new ReportStatusHistory
    {
        ChangedByUserId = reporterId,
        FromStatus = "Created",
        ToStatus = "Submitted",
        CreatedAt = DateTime.UtcNow
    });
    db.Reports.Add(report);
    await db.SaveChangesAsync();
    return Results.Created($"/api/reports/{report.Id}", new { report.Id, report.ReferenceNumber, report.Status, report.CreatedAt });
}).RequireAuthorization(policy => policy.RequireRole(CitizenRole));

app.MapPut("/api/reports/{id:int}/assignment", async (int id, AssignmentRequest request, ClaimsPrincipal user,
    CivicConnectDbContext db, UserManager<AppUser> users) =>
{
    var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == id);
    if (report is null) return Results.NotFound();
    var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var isAdmin = user.IsInRole(AdminRole);
    if (string.IsNullOrWhiteSpace(request.OfficerUserId) || request.DepartmentId <= 0 || request.Note?.Length > 500)
        return Results.BadRequest(new { message = "Choose an officer and department, and keep the note under 500 characters." });
    if (!isAdmin && (!user.IsInRole(OfficerRole) || request.OfficerUserId != currentUserId))
        return Results.Forbid();
    var officer = await users.FindByIdAsync(request.OfficerUserId);
    if (officer is null || !await users.IsInRoleAsync(officer, OfficerRole))
        return Results.BadRequest(new { message = "Choose an active officer account." });
    if (!await db.Departments.AnyAsync(department => department.Id == request.DepartmentId))
        return Results.BadRequest(new { message = "Choose a department." });
    if (officer.DepartmentId != request.DepartmentId)
        return Results.BadRequest(new { message = "The officer must belong to the selected department." });

    var previousStatus = report.Status;
    report.DepartmentId = request.DepartmentId;
    report.AssignedOfficerId = officer.Id;
    report.Status = "Assigned";
    report.UpdatedAt = DateTime.UtcNow;
    AddStatusHistory(report, currentUserId, previousStatus, report.Status, request.Note);
    db.Notifications.Add(new Notification
    {
        UserId = report.ReporterId,
        ReportId = report.Id,
        Message = $"Your report {report.ReferenceNumber} was assigned to {officer.DisplayName}.",
        CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { report.Id, report.Status, report.DepartmentId, report.AssignedOfficerId, report.UpdatedAt });
}).RequireAuthorization(policy => policy.RequireRole(OfficerRole, AdminRole));

app.MapPut("/api/reports/{id:int}/status", async (int id, StatusRequest request, ClaimsPrincipal user, CivicConnectDbContext db) =>
{
    var report = await db.Reports.FirstOrDefaultAsync(item => item.Id == id);
    if (report is null) return Results.NotFound();
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    if (!user.IsInRole(AdminRole) && report.AssignedOfficerId != userId)
        return Results.Forbid();
    var allowedStatuses = new[] { "Under review", "In progress", "Resolved", "Rejected" };
    if (!allowedStatuses.Contains(request.Status, StringComparer.Ordinal))
        return Results.BadRequest(new { message = "Choose a valid report status." });
    if (request.Note?.Length > 500)
        return Results.BadRequest(new { message = "The update note must be 500 characters or fewer." });

    var previousStatus = report.Status;
    report.Status = request.Status;
    report.UpdatedAt = DateTime.UtcNow;
    AddStatusHistory(report, userId, previousStatus, report.Status, request.Note?.Trim());
    db.Notifications.Add(new Notification
    {
        UserId = report.ReporterId,
        ReportId = report.Id,
        Message = $"Your report {report.ReferenceNumber} is now {report.Status.ToLowerInvariant()}.",
        CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { report.Id, report.Status, report.UpdatedAt });
}).RequireAuthorization(policy => policy.RequireRole(OfficerRole, AdminRole));

app.MapGet("/api/notifications", async (ClaimsPrincipal user, CivicConnectDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var notifications = await db.Notifications.AsNoTracking().Where(item => item.UserId == userId)
        .OrderByDescending(item => item.CreatedAt).Take(50)
        .Select(item => new { item.Id, item.ReportId, item.Message, item.CreatedAt, item.ReadAt }).ToListAsync();
    return Results.Ok(notifications);
}).RequireAuthorization();

app.MapPut("/api/notifications/{id:int}/read", async (int id, ClaimsPrincipal user, CivicConnectDbContext db) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var notification = await db.Notifications.FirstOrDefaultAsync(item => item.Id == id && item.UserId == userId);
    if (notification is null) return Results.NotFound();
    notification.ReadAt ??= DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

var adminRoutes = app.MapGroup("/api/admin").RequireAuthorization(policy => policy.RequireRole(AdminRole));
adminRoutes.MapGet("/areas", async (CivicConnectDbContext db) =>
    Results.Ok(await db.PilotAreas.OrderBy(area => area.District).ThenBy(area => area.Name)
        .Select(area => new { area.Id, area.Name, area.District }).ToListAsync()));
adminRoutes.MapPost("/areas", async (PilotAreaRequest request, CivicConnectDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120 ||
        string.IsNullOrWhiteSpace(request.District) || request.District.Trim().Length > 120)
        return Results.BadRequest(new { message = "Enter a pilot area name and district, each up to 120 characters." });
    if (await db.PilotAreas.AnyAsync(area => area.Name.ToLower() == request.Name.Trim().ToLower() && area.District.ToLower() == request.District.Trim().ToLower()))
        return Results.Conflict(new { message = "That pilot area already exists." });
    var area = new PilotArea { Name = request.Name.Trim(), District = request.District.Trim() };
    db.PilotAreas.Add(area);
    await db.SaveChangesAsync();
    return Results.Created($"/api/admin/areas/{area.Id}", new { area.Id, area.Name, area.District });
});
adminRoutes.MapDelete("/areas/{id:int}", async (int id, CivicConnectDbContext db) =>
{
    var area = await db.PilotAreas.FindAsync(id);
    if (area is null) return Results.NotFound();
    if (await db.Reports.AnyAsync(report => report.PilotAreaId == id))
        return Results.Conflict(new { message = "This pilot area is already used by reports, so it can’t be removed." });
    db.PilotAreas.Remove(area);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
adminRoutes.MapGet("/departments", async (CivicConnectDbContext db) =>
    Results.Ok(await db.Departments.OrderBy(department => department.Name)
        .Select(department => new { department.Id, department.Name, department.Description }).ToListAsync()));
adminRoutes.MapPost("/departments", async (DepartmentRequest request, CivicConnectDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        return Results.BadRequest(new { message = "Enter a department name up to 120 characters." });
    if (await db.Departments.AnyAsync(department => department.Name.ToLower() == request.Name.Trim().ToLower()))
        return Results.Conflict(new { message = "That department already exists." });
    var department = new Department { Name = request.Name.Trim(), Description = request.Description?.Trim() };
    db.Departments.Add(department);
    await db.SaveChangesAsync();
    return Results.Created($"/api/admin/departments/{department.Id}", new { department.Id, department.Name, department.Description });
});
adminRoutes.MapDelete("/departments/{id:int}", async (int id, CivicConnectDbContext db) =>
{
    var department = await db.Departments.FindAsync(id);
    if (department is null) return Results.NotFound();
    if (await db.Users.OfType<AppUser>().AnyAsync(user => user.DepartmentId == id))
        return Results.Conflict(new { message = "Move the department’s officers before removing it." });
    db.Departments.Remove(department);
    await db.SaveChangesAsync();
    return Results.NoContent();
});
adminRoutes.MapPost("/officers", async (OfficerRequest request, CivicConnectDbContext db, UserManager<AppUser> users) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || request.DepartmentId <= 0)
        return Results.BadRequest(new { message = "Enter the officer’s name, email, and department." });
    if (!await db.Departments.AnyAsync(department => department.Id == request.DepartmentId))
        return Results.BadRequest(new { message = "Choose a department." });
    var email = request.Email.Trim();
    if (await users.FindByEmailAsync(email) is not null)
        return Results.Conflict(new { message = "An account already uses that email." });
    var officer = new AppUser { UserName = email, Email = email, DisplayName = request.DisplayName.Trim(), DepartmentId = request.DepartmentId };
    var created = await users.CreateAsync(officer, request.Password ?? string.Empty);
    if (!created.Succeeded)
        return Results.BadRequest(new { message = string.Join(" ", created.Errors.Select(error => error.Description)) });
    await users.AddToRoleAsync(officer, OfficerRole);
    return Results.Created($"/api/admin/officers/{officer.Id}", new { officer.Id, officer.Email, officer.DisplayName, officer.DepartmentId });
});
adminRoutes.MapGet("/officers", async (UserManager<AppUser> users, CivicConnectDbContext db) =>
{
    var officers = await users.GetUsersInRoleAsync(OfficerRole);
    var departments = await db.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.Name);
    return Results.Ok(officers.Select(officer => new
    {
        officer.Id, officer.Email, officer.DisplayName, officer.DepartmentId,
        Department = officer.DepartmentId is int id && departments.TryGetValue(id, out var name) ? name : null
    }));
});
adminRoutes.MapPut("/officers/{id}/department", async (string id, OfficerDepartmentRequest request,
    UserManager<AppUser> users, CivicConnectDbContext db) =>
{
    var officer = await users.FindByIdAsync(id);
    if (officer is null || !await users.IsInRoleAsync(officer, OfficerRole)) return Results.NotFound();
    if (request.DepartmentId is int departmentId && !await db.Departments.AnyAsync(department => department.Id == departmentId))
        return Results.BadRequest(new { message = "Choose a valid department." });
    officer.DepartmentId = request.DepartmentId;
    await users.UpdateAsync(officer);
    return Results.NoContent();
});
adminRoutes.MapPost("/categories", async (CategoryRequest request, CivicConnectDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80)
        return Results.BadRequest(new { message = "Enter a category name up to 80 characters." });
    if (await db.Categories.AnyAsync(category => category.Name.ToLower() == request.Name.Trim().ToLower()))
        return Results.Conflict(new { message = "That issue category already exists." });
    var category = new IssueCategory { Name = request.Name.Trim(), Description = request.Description?.Trim() ?? string.Empty };
    db.Categories.Add(category);
    await db.SaveChangesAsync();
    return Results.Created($"/api/categories/{category.Id}", new { category.Id, category.Name, category.Description });
});
adminRoutes.MapPut("/categories/{id:int}", async (int id, CategoryRequest request, CivicConnectDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 80)
        return Results.BadRequest(new { message = "Enter a category name up to 80 characters." });
    category.Name = request.Name.Trim();
    category.Description = request.Description?.Trim() ?? string.Empty;
    await db.SaveChangesAsync();
    return Results.Ok(new { category.Id, category.Name, category.Description });
});
adminRoutes.MapDelete("/categories/{id:int}", async (int id, CivicConnectDbContext db) =>
{
    var category = await db.Categories.FindAsync(id);
    if (category is null) return Results.NotFound();
    if (await db.Reports.AnyAsync(report => report.CategoryId == id))
        return Results.Conflict(new { message = "This category is already used by reports, so it can’t be removed." });
    db.Categories.Remove(category);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

static void AddStatusHistory(IssueReport report, string userId, string from, string to, string? note) =>
    report.StatusHistory.Add(new ReportStatusHistory
    {
        ChangedByUserId = userId,
        FromStatus = from,
        ToStatus = to,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        CreatedAt = DateTime.UtcNow
    });

static object CreateToken(AppUser user, IList<string> roles, string signingKey)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Name, user.DisplayName),
        new(ClaimTypes.Email, user.Email ?? string.Empty)
    };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
    if (user.DepartmentId is int departmentId) claims.Add(new Claim("department_id", departmentId.ToString()));
    var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken("CivicConnect", "CivicConnect", claims,
        expires: DateTime.UtcNow.AddHours(6), signingCredentials: credentials);
    return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = token.ValidTo,
        user = new { user.Id, user.DisplayName, user.Email, roles, user.DepartmentId } };
}

static async Task<bool> HasMatchingImageSignatureAsync(IFormFile file)
{
    var header = new byte[12];
    await using var stream = file.OpenReadStream();
    var read = await stream.ReadAsync(header);
    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        return read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    if (file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        return read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    return read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
}

record RegisterRequest(string? DisplayName, string? Email, string? Password);
record LoginRequest(string? Email, string? Password);
record AssignmentRequest(string OfficerUserId, int DepartmentId, string? Note);
record StatusRequest(string Status, string? Note);
record DepartmentRequest(string? Name, string? Description);
record PilotAreaRequest(string? Name, string? District);
record OfficerRequest(string? Email, string? DisplayName, string? Password, int DepartmentId);
record OfficerDepartmentRequest(int? DepartmentId);
record CategoryRequest(string? Name, string? Description);
