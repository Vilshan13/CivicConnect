namespace CivicConnect.Api.Models;

public sealed class IssueReport
{
    public int Id { get; set; }
    public required string ReferenceNumber { get; set; }
    public int CategoryId { get; set; }
    public IssueCategory Category { get; set; } = null!;
    public int PilotAreaId { get; set; }
    public PilotArea PilotArea { get; set; } = null!;
    public required string Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PhotoFileName { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public required string ReporterId { get; set; }
    public AppUser Reporter { get; set; } = null!;
    public string? AssignedOfficerId { get; set; }
    public AppUser? AssignedOfficer { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public List<ReportStatusHistory> StatusHistory { get; set; } = [];
}
