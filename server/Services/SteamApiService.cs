namespace API.Services;

using System.Text.Json;
using Models;

public interface ISteamApiService
{
    Task<SteamWishlistResponse?> GetUserWishlistAsync(string steamId);
    Task<SteamGame?> GetGameDetailsAsync(string appId);
}

public class SteamApiService : ISteamApiService
{
    private static string? _steamApiUrl = Environment.GetEnvironmentVariable("STEAM_API_URL");
    private static string? _steamApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY");
    private const string steamApiUrlTemplate = "{0}?key={1}&steamids=[{2}]";

    public static async Task<SteamPlayer> SyncUserWithSteamAsync(string userSteamId)
    {
        if (string.IsNullOrWhiteSpace(userSteamId))
        {
            throw new ArgumentException("Invalid user Steam ID.");
        }

        using HttpClient httpClient = new HttpClient();
        HttpResponseMessage response = await httpClient.GetAsync(string.Format(steamApiUrlTemplate, _steamApiUrl, _steamApiKey, userSteamId));

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to fetch data from Steam API.");
        }

        string content = await response.Content.ReadAsStringAsync();
        SteamApiResponse<GetPlayersResponse> steamApiResponse = JsonSerializer.Deserialize<SteamApiResponse<GetPlayersResponse>>(content);

        if (steamApiResponse?.Response?.Players == null || steamApiResponse.Response.Players.Count == 0)
        {
            throw new Exception("No player data found for the given Steam ID.");
        }

        SteamPlayer player = steamApiResponse.Response.Players.First();
        return player;
    }

    public async Task<SteamWishlistResponse?> GetUserWishlistAsync(string steamId)
    {
        try
        {
            if (string.IsNullOrEmpty(_steamApiKey))
            {
                throw new InvalidOperationException("STEAM_API_KEY environment variable is not set.");
            }

            using HttpClient httpClient = new HttpClient();
            var url = $"https://api.steampowered.com/IWishlistService/GetWishlist/v1?steamid={steamId}&key={_steamApiKey}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<SteamWishlistResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<SteamGame?> GetGameDetailsAsync(string appId)
    {
        try
        {
            using HttpClient httpClient = new HttpClient();
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            var appDetailsResponse = JsonSerializer.Deserialize<Dictionary<string, SteamAppDetailsContainer>>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (appDetailsResponse != null &&
                appDetailsResponse.TryGetValue(appId, out var appDetails) &&
                appDetails.success &&
                appDetails.data != null)
            {
                return appDetails.data;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}