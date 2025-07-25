namespace API.Models;

public class SteamAppDetailsResponse
{
    public Dictionary<string, SteamAppDetailsContainer> AppDetails { get; set; } = new();
}

public class SteamAppDetailsContainer
{
    public bool success { get; set; }
    public SteamGame? data { get; set; }
}
