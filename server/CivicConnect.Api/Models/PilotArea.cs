namespace CivicConnect.Api.Models;

public sealed class PilotArea
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string District { get; set; }
    public List<IssueReport> Reports { get; set; } = [];
}
