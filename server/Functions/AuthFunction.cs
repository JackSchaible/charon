namespace API.Functions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda;
using Services;
using System.Text.Json;
using Amazon.Lambda.Model;
using Entities;
using Repositories;
using Environment = Environment;

public class AuthFunction
{
    private readonly SteamAuthService _steamAuthService;
    private readonly UserRepository _userRepository;
    private readonly JobRepository _jobRepository;
    private readonly JwtService _jwtService;
    private readonly AmazonLambdaClient _lambdaClient;
    private readonly string? _steamApiKey;
    private readonly string? _baseUrl;
    private readonly string? _syncFunctionName;
    private readonly string? _dbConnectionString;

    public AuthFunction()
    {
        HttpClient httpClient = new HttpClient();
        _steamAuthService = new SteamAuthService(httpClient);

        _dbConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set");
        _userRepository = new UserRepository(_dbConnectionString);
        _jobRepository = new JobRepository(_dbConnectionString);

        _steamApiKey = Environment.GetEnvironmentVariable("STEAM_API_KEY") ?? throw new InvalidOperationException("STEAM_API_KEY environment variable is not set");
        _baseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? throw new InvalidOperationException("BASE_URL environment variable is not set");
        _syncFunctionName = Environment.GetEnvironmentVariable("SYNC_FUNCTION_NAME") ?? "WishlistSyncFunction";

        string jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET environment variable is not set");
        string jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "charon-api";
        string jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "charon-client";
        _jwtService = new JwtService(jwtSecret, jwtIssuer, jwtAudience);

        _lambdaClient = new AmazonLambdaClient();
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
                "/auth/sync" when request.HttpMethod == "POST" => await HandleSyncAsync(request, context),
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
        if (string.IsNullOrWhiteSpace(_baseUrl))
        {
            context.Logger.LogLine("BASE_URL environment variable not set");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "BASE_URL environment variable is not set" }),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                }
            };
        }

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

        if (!isValid || string.IsNullOrWhiteSpace(steamId))
        {
            string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Steam authentication failed")}";
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
        if (string.IsNullOrWhiteSpace(_steamApiKey))
        {
            context.Logger.LogLine("STEAM_API_KEY environment variable not set");
            string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Steam API key not configured")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        if (string.IsNullOrWhiteSpace(_dbConnectionString))
        {
            context.Logger.LogLine("DB_CXN_STRING environment variable not set");
            string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Database connection string not configured")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        User? user = await _steamAuthService.GetSteamUserInfoAsync(steamId, _steamApiKey, _dbConnectionString, _userRepository);
        if (user == null)
        {
            string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Failed to retrieve user information")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }

        try
        {
            // Save or update user in database
            User savedUser = await _userRepository.UpsertUserAsync(user);
            context.Logger.LogLine($"User saved/updated in database: {savedUser.Username} (ID: {savedUser.Id})");

            if (string.IsNullOrWhiteSpace(savedUser.SteamId) || string.IsNullOrWhiteSpace(savedUser.Username))
            {
                context.Logger.LogLine("Failed to save user information properly");
                // Redirect to error page
                string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Failed to save user information")}";
                return new APIGatewayProxyResponse
                {
                    StatusCode = 302,
                    Headers = new Dictionary<string, string>
                    {
                        { "Location", errorUrl }
                    }
                };
            }

            // Generate JWT token
            string jwtToken = _jwtService.GenerateToken(
                savedUser.Id.ToString(),
                savedUser.SteamId,
                savedUser.Username
            );

            Job job = await _jobRepository.CreateJobAsync(savedUser.Id);

            // Trigger wishlist sync function asynchronously
            try
            {
                InvokeRequest invokeRequest = new()
                {
                    FunctionName = _syncFunctionName,
                    InvocationType = InvocationType.Event,
                    Payload = JsonSerializer.Serialize(new { jobId = job.Id, force = false }),
                };

                await _lambdaClient.InvokeAsync(invokeRequest);
                context.Logger.LogLine($"Triggered wishlist sync for user {savedUser.Id}");
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Failed to trigger sync function: {ex.Message}");
                // Don't fail the auth process if sync trigger fails
            }

            // Redirect to client with JWT token
            string successUrl = $"{returnUrl}/callback?token={Uri.EscapeDataString(jwtToken)}";

            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", successUrl }
                }
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Database error: {ex.Message}");
            string errorUrl = $"{returnUrl}/callback?error={Uri.EscapeDataString("Database error occurred")}";
            return new APIGatewayProxyResponse
            {
                StatusCode = 302,
                Headers = new Dictionary<string, string>
                {
                    { "Location", errorUrl }
                }
            };
        }
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

    private async Task<APIGatewayProxyResponse> HandleSyncAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Extract JWT token from Authorization header
            string? authHeader = request.Headers?.TryGetValue("Authorization", out var authHeaderValue) == true ? authHeaderValue : null;
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 401,
                    Body = JsonSerializer.Serialize(new { error = "Authorization header required" }),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" }
                    }
                };
            }

            string token = authHeader.Substring("Bearer ".Length);
            var principal = _jwtService.ValidateToken(token);
            if (principal == null)
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 401,
                    Body = JsonSerializer.Serialize(new { error = "Invalid token" }),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" }
                    }
                };
            }

            // Extract user ID from token
            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 401,
                    Body = JsonSerializer.Serialize(new { error = "Invalid user ID in token" }),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" }
                    }
                };
            }

            // Parse force parameter from request body
            bool force = false;
            if (!string.IsNullOrEmpty(request.Body))
            {
                try
                {
                    var requestData = JsonSerializer.Deserialize<JsonElement>(request.Body);
                    if (requestData.TryGetProperty("force", out var forceElement))
                    {
                        force = forceElement.GetBoolean();
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogLine($"Error parsing request body: {ex.Message}");
                }
            }

            context.Logger.LogLine($"Starting sync for user {userId}, force: {force}");

            // Create a new job for this sync
            Job job = await _jobRepository.CreateJobAsync(userId);

            // Trigger wishlist sync function asynchronously
            try
            {
                InvokeRequest invokeRequest = new()
                {
                    FunctionName = _syncFunctionName,
                    InvocationType = InvocationType.Event,
                    Payload = JsonSerializer.Serialize(new { jobId = job.Id, force }),
                };

                await _lambdaClient.InvokeAsync(invokeRequest);
                context.Logger.LogLine($"Triggered wishlist sync for user {userId}, job {job.Id}");

                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "Sync started",
                        jobId = job.Id
                    }),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" },
                        { "Access-Control-Allow-Headers", "Content-Type, Authorization" },
                        { "Access-Control-Allow-Methods", "GET, POST, OPTIONS" }
                    }
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Failed to trigger sync function: {ex.Message}");

                // Update job status to error
                await _jobRepository.UpdateJobStatusAsync(job.Id, "ERROR", $"Failed to trigger sync: {ex.Message}");

                return new APIGatewayProxyResponse
                {
                    StatusCode = 500,
                    Body = JsonSerializer.Serialize(new { error = "Failed to start sync", details = ex.Message }),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" },
                        { "Access-Control-Allow-Origin", "*" }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error in HandleSyncAsync: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "Internal server error", details = ex.Message }),
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Access-Control-Allow-Origin", "*" }
                }
            };
        }
    }
}