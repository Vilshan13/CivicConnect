namespace CivicConnect.Api.Models;

public sealed class IssueReport
{
    public int Id { get; set; }
    public required string ReferenceNumber { get; set; }
    public int CategoryId { get; set; }
    public IssueCategory Category { get; set; } = null!;
    public required string Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? PhotoUrl { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
