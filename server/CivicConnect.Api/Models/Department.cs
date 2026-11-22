namespace CivicConnect.Api.Models;

public sealed class Department
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<IssueReport> Reports { get; set; } = [];
}
