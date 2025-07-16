namespace API.Entities;

public class WishlistItem
{
    public int UserId { get; set; }
    public string? GameId { get; set; }
    public string? Notes { get; set; }
    public DateTime? AddedAt { get; set; }
}
