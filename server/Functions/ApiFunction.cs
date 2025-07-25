namespace API.Functions;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;
using Repositories;
using Services;

public class ApiFunction
{
    private readonly string _connectionString;
    private readonly IJobRepository _jobRepository;
    private readonly IWishlistRepository _wishlistRepository;
    private readonly JwtService _jwtService;

    public ApiFunction()
    {
        _connectionString = Environment.GetEnvironmentVariable("ConnectionString") ??
            throw new InvalidOperationException("ConnectionString environment variable is not set.");

        _jobRepository = new JobRepository(_connectionString);
        _wishlistRepository = new WishlistRepository(_connectionString);
        _jwtService = new JwtService();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {request.HttpMethod} {request.Path}");

        try
        {
            return request.Path switch
            {
                "/api/ping" => HandlePing(),
                var path when path.StartsWith("/api/jobs/") => await HandleJobStatus(request, context),
                "/api/wishlist" when request.HttpMethod == "GET" => await HandleGetWishlist(request, context),
                _ => HandleNotFound()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error processing request: {ex.Message}");
            return CreateErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    private static APIGatewayProxyResponse HandlePing()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Hello from API Function!",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }

    private async Task<APIGatewayProxyResponse> HandleJobStatus(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Extract job ID from path like /api/jobs/{jobId}
            var pathParts = request.Path.Split('/');
            if (pathParts.Length < 4 || !long.TryParse(pathParts[3], out var jobId))
            {
                return CreateErrorResponse(400, "Invalid job ID");
            }

            var job = await _jobRepository.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return CreateErrorResponse(404, "Job not found");
            }

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(job),
                Headers = CreateCorsHeaders()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error getting job status: {ex.Message}");
            return CreateErrorResponse(500, "Failed to get job status", ex.Message);
        }
    }

    private async Task<APIGatewayProxyResponse> HandleGetWishlist(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Extract user ID from JWT token
            var authHeader = request.Headers?.TryGetValue("Authorization", out var authHeaderValue) == true ? authHeaderValue : null;
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return CreateErrorResponse(401, "Authorization header required");
            }

            var token = authHeader.Substring("Bearer ".Length);
            var userId = _jwtService.GetUserIdFromToken(token);
            if (userId == null)
            {
                return CreateErrorResponse(401, "Invalid token");
            }

            var wishlist = await _wishlistRepository.GetUserWishlistAsync(userId.Value);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(wishlist),
                Headers = CreateCorsHeaders()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error getting wishlist: {ex.Message}");
            return CreateErrorResponse(500, "Failed to get wishlist", ex.Message);
        }
    }

    private static APIGatewayProxyResponse HandleNotFound()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 404,
            Body = "Not Found",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
    }

    private static APIGatewayProxyResponse CreateErrorResponse(int statusCode, string message, string? details = null)
    {
        object response;
        if (!string.IsNullOrEmpty(details))
        {
            response = new { error = message, details };
        }
        else
        {
            response = new { error = message };
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(response),
            Headers = CreateCorsHeaders()
        };
    }

    private static Dictionary<string, string> CreateCorsHeaders()
    {
        return new Dictionary<string, string>
        {
            { "Content-Type", "application/json" },
            { "Access-Control-Allow-Origin", "*" },
            { "Access-Control-Allow-Headers", "Content-Type, Authorization" },
            { "Access-Control-Allow-Methods", "GET, POST, OPTIONS" }
        };
    }
}