namespace CivicConnect.Api.Models;

public sealed class IssueCategory
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public List<IssueReport> Reports { get; set; } = [];
}
