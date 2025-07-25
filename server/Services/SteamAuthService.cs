namespace API.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using Entities;
using Models;
using Repositories;

public partial class SteamAuthService(HttpClient httpClient)
{
    private const string SteamOpenIdEndpoint = "https://steamcommunity.com/openid/login";
    private const string SteamApiBaseUrl = "https://api.steampowered.com";

    public static string GenerateAuthUrl(string returnUrl, string realm)
    {
        Dictionary<string, string> parameters = new()
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnUrl,
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select"
        };

        string queryString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{SteamOpenIdEndpoint}?{queryString}";
    }

    public async Task<(bool IsValid, string? SteamId)> ValidateAuthenticationAsync(
        Dictionary<string, string> parameters)
    {
        try
        {
            // Verify the response with Steam
            Dictionary<string, string> validationParams = new(parameters)
            {
                ["openid.mode"] = "check_authentication"
            };

            FormUrlEncodedContent formContent = new FormUrlEncodedContent(validationParams);
            HttpResponseMessage response = await httpClient.PostAsync(SteamOpenIdEndpoint, formContent);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!responseContent.Contains("is_valid:true"))
                return (false, null);

            // Extract Steam ID from the identity URL
            if (!parameters.TryGetValue("openid.identity", out string? identity)) return (false, null);

            Match steamIdMatch = DigitRegex().Match(identity);
            return steamIdMatch.Success ? (true, steamIdMatch.Groups[1].Value) : (false, null);
        }
        catch
        {
            return (false, null);
        }
    }

    public async Task<User?> GetSteamUserInfoAsync(string steamId, string steamApiKey, string dbCxnString, UserRepository userRepository)
    {
        SteamPlayer? player = await SteamApiService.SyncUserWithSteamAsync(steamId);

        User? existingUser = await userRepository.GetUserBySteamIdAsync(steamId);
        if (existingUser != null)
        {
            // Update existing user
            existingUser.Username = player.personaname;
            existingUser.AvatarUrl = player.avatarmedium;
            existingUser.ProfileUrl = player.profileurl;
            return await userRepository.UpdateUserAsync(existingUser);
        }
        else
        {
            // Create new user
            User newUser = new()
            {
                SteamId = steamId,
                Username = player.personaname,
                AvatarUrl = player.avatarmedium,
                ProfileUrl = player.profileurl,
                CreatedAt = DateTime.UtcNow
            };
            return await userRepository.CreateUserAsync(newUser);
        }
    }

    [GeneratedRegex(@"(\d+)$")]
    private static partial Regex DigitRegex();
}