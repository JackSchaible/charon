namespace API.Functions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Services;
using System.Text.Json;
using Entities;
using Repositories;

public class AuthFunction
{
    private readonly SteamAuthService _steamAuthService;
    private readonly IUserRepository _userRepository;
    private readonly string? _steamApiKey;
    private readonly string? _baseUrl;

    public AuthFunction()
    {
        HttpClient httpClient = new HttpClient();
        _steamAuthService = new SteamAuthService(httpClient);

        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set");
        _userRepository = new UserRepository(connectionString);

        _steamApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY") ?? throw new InvalidOperationException("STEAM_API_KEY environment variable is not set");
        _baseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? throw new InvalidOperationException("BASE_URL environment variable is not set");
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {request.HttpMethod} {request.Path}");

        try
        {
            return request.Path switch
            {
                "/auth/ping" => HandlePing(),
                "/auth/login" => HandleLogin(request, context),
                "/auth/callback" => await HandleCallbackAsync(request, context),
                _ => HandleNotFound()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "Internal server error" }),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" },
                    { "Access-Control-Allow-Headers", "Content-Type" },
                    { "Access-Control-Allow-Methods", "GET, POST, OPTIONS" }
                }
            };
        }
    }

    private APIGatewayProxyResponse HandlePing()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Hello from Auth Function!",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }

    private APIGatewayProxyResponse HandleLogin(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? returnUrl = null;
        request.QueryStringParameters?.TryGetValue("returnUrl", out returnUrl);

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = JsonSerializer.Serialize(new { error = "returnUrl parameter is required" }),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                }
            };
        }

        string callbackUrl = $"{_baseUrl}/auth/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
        string steamAuthUrl = SteamAuthService.GenerateAuthUrl(callbackUrl, _baseUrl);

        context.Logger.LogLine($"Redirecting to Steam: {steamAuthUrl}");

        return new APIGatewayProxyResponse
        {
            StatusCode = 302,
            Headers = new Dictionary<string, string>
            {
                { "Location", steamAuthUrl },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }

    private async Task<APIGatewayProxyResponse> HandleCallbackAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        string? returnUrl = null;
        request.QueryStringParameters?.TryGetValue("returnUrl", out returnUrl);

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = "Missing returnUrl parameter",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "text/plain" }
                }
            };
        }

        // Extract OpenID parameters from query string
        Dictionary<string, string> openIdParams = new Dictionary<string, string>();
        if (request.QueryStringParameters != null)
        {
            foreach (KeyValuePair<string, string> param in request.QueryStringParameters)
            {
                if (param.Key.StartsWith("openid."))
                {
                    openIdParams[param.Key] = param.Value;
                }
            }
        }

        context.Logger.LogLine($"OpenID parameters: {JsonSerializer.Serialize(openIdParams)}");

        // Validate with Steam
        (bool isValid, string? steamId) = await _steamAuthService.ValidateAuthenticationAsync(openIdParams);

        if (!isValid || string.IsNullOrEmpty(steamId))
        {
            string errorUrl = $"{returnUrl}?error={Uri.EscapeDataString("Steam authentication failed")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        // Get user info from Steam API
        if (string.IsNullOrEmpty(_steamApiKey))
        {
            context.Logger.LogLine("STEAM_API_KEY environment variable not set");
            string errorUrl = $"{returnUrl}?error={Uri.EscapeDataString("Steam API key not configured")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        User? user = await _steamAuthService.GetSteamUserInfoAsync(steamId, _steamApiKey);
        if (user == null)
        {
            string errorUrl = $"{returnUrl}?error={Uri.EscapeDataString("Failed to retrieve user information")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        // Save or update user in database
        try
        {
            user = await _userRepository.UpsertUserAsync(user);
            context.Logger.LogLine($"User saved/updated in database: {user.Username} (ID: {user.Id}, SteamId: {user.SteamId})");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Database error: {ex.Message}");
            string errorUrl = $"{returnUrl}?error={Uri.EscapeDataString("Database error occurred")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        // Create user data to pass back
        string userData = JsonSerializer.Serialize(new
        {
            Id = user.Id,
            SteamId = user.SteamId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            ProfileUrl = user.ProfileUrl,
            CreatedAt = user.CreatedAt?.ToString("O")
        });

        string successUrl = $"{returnUrl}?user={Uri.EscapeDataString(userData)}";

        return new APIGatewayProxyResponse
        {
            StatusCode = 302,
            Headers = new Dictionary<string, string>
            {
                { "Location", successUrl }
            }
        };
    }

    private APIGatewayProxyResponse HandleNotFound()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 404,
            Body = JsonSerializer.Serialize(new { error = "Not Found" }),
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }
}