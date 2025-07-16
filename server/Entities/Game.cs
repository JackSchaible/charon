namespace API.Entities;

public class Game
{
    public string? AppId { get; set; }
    public string? Title { get; set; }
    public string? ImageUrl { get; set; }
    public string? Price { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public DateTime? LastFetched { get; set; }
}
