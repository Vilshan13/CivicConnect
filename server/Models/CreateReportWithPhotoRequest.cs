using Microsoft.AspNetCore.Http;

namespace CivicConnect.Api.Models;

public sealed class CreateReportWithPhotoRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public IFormFile? Photo { get; set; }
}
