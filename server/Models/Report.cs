using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Api.Models;

public class Report
{
    public ICollection<ReportStatusHistory> StatusHistory { get; set; }
    = new List<ReportStatusHistory>();

    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AreaName { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = "Submitted";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}