namespace VetClaims.Api.Auth;

public sealed class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Reviewer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
