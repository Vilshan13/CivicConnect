namespace CivicConnect.Api.Models;

public sealed class ReportStatusHistory
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public IssueReport Report { get; set; } = null!;
    public required string ChangedByUserId { get; set; }
    public required string FromStatus { get; set; }
    public required string ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
