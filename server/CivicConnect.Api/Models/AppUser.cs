using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Api.Models;

public sealed class AppUser : IdentityUser
{
    public required string DisplayName { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public List<IssueReport> Reports { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
}
