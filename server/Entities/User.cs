namespace API.Entities;

public class User
{
    public int Id { get; set; }
    public string? SteamId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? AvatarUrl { get; set; }
    public string? ProfileUrl { get; set; }
    public string? Username { get; set; }
}