using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CivicConnect.Api.Models;

public class ReportStatusHistory
{
    public int Id { get; set; }

    public int ReportId { get; set; }

    [MaxLength(30)]
    public string? PreviousStatus { get; set; }

    [Required]
    [MaxLength(30)]
    public string NewStatus { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Report Report { get; set; } = null!;
}
