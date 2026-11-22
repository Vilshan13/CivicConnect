namespace CivicConnect.Api.Models;

public class UpdateReportStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
}