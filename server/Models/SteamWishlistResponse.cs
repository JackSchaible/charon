namespace API.Models;

public class SteamWishlistResponse
{
    public Dictionary<string, SteamWishlistItem> wishlist { get; set; } = new();
    public int success { get; set; }
}
