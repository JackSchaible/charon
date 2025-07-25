namespace API.Models;

public class GetWishlistResponse
{
    public List<SteamWishlistItem> Wishlist { get; set; } = new();
}