namespace CivicConnect.Api.Models;

public class RegisterCitizenRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}