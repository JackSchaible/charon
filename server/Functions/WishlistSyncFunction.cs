namespace API.Functions;

using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Repositories;
using Services;
using Models;
using Entities;

public class WishlistSyncFunction
{
    private readonly string _connectionString;
    private readonly IJobRepository _jobRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IWishlistRepository _wishlistRepository;
    private readonly ISteamApiService _steamApiService;

    public WishlistSyncFunction()
    {
        _connectionString = Environment.GetEnvironmentVariable("ConnectionString") ??
            throw new InvalidOperationException("ConnectionString environment variable is not set.");

        _jobRepository = new JobRepository(_connectionString);
        _userRepository = new UserRepository(_connectionString);
        _gameRepository = new GameRepository(_connectionString);
        _wishlistRepository = new WishlistRepository(_connectionString);
        _steamApiService = new SteamApiService();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        context.Logger.LogLine($"Received request: {request.HttpMethod} {request.Path}");

        try
        {
            return request.Path switch
            {
                "/sync/ping" => HandlePing(),
                "/sync/wishlist" when request.HttpMethod == "POST" => await HandleWishlistSync(request, context),
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

    private static APIGatewayProxyResponse HandlePing()
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = "Pong",
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "text/plain" },
                { "Access-Control-Allow-Origin", "*" }
            }
        };
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

    private async Task<APIGatewayProxyResponse> HandleWishlistSync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            // Parse the request body to get job ID and optional force parameter
            if (string.IsNullOrEmpty(request.Body))
            {
                return CreateErrorResponse(400, "Request body is required");
            }

            var requestData = JsonSerializer.Deserialize<JsonElement>(request.Body);

            if (!requestData.TryGetProperty("jobId", out var jobIdElement))
            {
                return CreateErrorResponse(400, "jobId is required");
            }

            long jobId = jobIdElement.GetInt64();
            bool force = requestData.TryGetProperty("force", out var forceElement) && forceElement.GetBoolean();

            context.Logger.LogLine($"Starting wishlist sync for job {jobId}, force: {force}");

            // Get the job and validate it exists
            var job = await _jobRepository.GetJobByIdAsync(jobId);
            if (job == null)
            {
                return CreateErrorResponse(404, "Job not found");
            }

            // Update job status to RUNNING
            await _jobRepository.UpdateJobStatusAsync(jobId, "RUN", "Starting wishlist synchronization");

            // Get user by user ID from the job
            var user = await _userRepository.GetUserByIdAsync((int)job.UserId);
            if (user?.SteamId == null)
            {
                await _jobRepository.UpdateJobStatusAsync(jobId, "ERROR", "User or Steam ID not found");
                return CreateErrorResponse(404, "User or Steam ID not found");
            }

            context.Logger.LogLine($"Syncing wishlist for user {user.Id}, Steam ID: {user.SteamId}");

            // Get user's wishlist from Steam API
            var wishlistResponse = await _steamApiService.GetUserWishlistAsync(user.SteamId);
            if (wishlistResponse?.wishlist == null)
            {
                await _jobRepository.UpdateJobStatusAsync(jobId, "ERROR", "Failed to fetch wishlist from Steam API");
                return CreateErrorResponse(500, "Failed to fetch wishlist from Steam API");
            }

            await _jobRepository.UpdateJobStatusAsync(jobId, "RUN", $"Found {wishlistResponse.wishlist.Count} items in wishlist");

            int processedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            // Clear existing wishlist items for this user
            await _wishlistRepository.ClearUserWishlistAsync(user.Id);

            foreach (var wishlistItem in wishlistResponse.wishlist)
            {
                string appId = wishlistItem.Key;
                var itemData = wishlistItem.Value;

                try
                {
                    // Check if game already exists in database (unless force is true)
                    if (!force && await _gameRepository.GameExistsAsync(appId))
                    {
                        context.Logger.LogLine($"Skipping existing game: {appId}");
                        skippedCount++;
                    }
                    else
                    {
                        // Fetch game details from Steam API
                        var gameDetails = await _steamApiService.GetGameDetailsAsync(appId);
                        if (gameDetails != null)
                        {
                            // Create Game entity from Steam data
                            var game = new Game
                            {
                                AppId = appId,
                                Title = gameDetails.name,
                                ImageUrl = gameDetails.header_image,
                                Price = gameDetails.price_overview?.final_formatted ?? "N/A",
                                ReleaseDate = ParseReleaseDate(gameDetails.release_date?.date),
                                LastFetched = DateTime.UtcNow
                            };

                            // Upsert game into database
                            await _gameRepository.UpsertGameAsync(game);
                            context.Logger.LogLine($"Processed game: {appId} - {game.Title}");
                            processedCount++;
                        }
                        else
                        {
                            context.Logger.LogLine($"Failed to fetch details for game: {appId}");
                            errorCount++;
                        }

                        // Add small delay to respect rate limits
                        await Task.Delay(100);
                    }

                    // Add to user's wishlist
                    var wishlistEntry = new WishlistItem
                    {
                        UserId = user.Id,
                        GameId = appId,
                        AddedAt = DateTimeOffset.FromUnixTimeSeconds(itemData.date_added).DateTime
                    };
                    await _wishlistRepository.UpsertWishlistItemAsync(wishlistEntry);

                    // Update progress periodically
                    if ((processedCount + skippedCount + errorCount) % 10 == 0)
                    {
                        await _jobRepository.UpdateJobStatusAsync(jobId, "RUN",
                            $"Progress: {processedCount + skippedCount + errorCount}/{wishlistResponse.wishlist.Count} items processed");
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogLine($"Error processing game {appId}: {ex.Message}");
                    errorCount++;
                }
            }

            // Update job status to completed
            string finalDetails = $"Sync completed. Processed: {processedCount}, Skipped: {skippedCount}, Errors: {errorCount}";
            await _jobRepository.UpdateJobStatusAsync(jobId, "COMPLETE", finalDetails);

            context.Logger.LogLine($"Wishlist sync completed for job {jobId}. {finalDetails}");

            // Return success with user's updated wishlist
            var userWishlist = await _wishlistRepository.GetUserWishlistAsync(user.Id);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new
                {
                    success = true,
                    message = finalDetails,
                    wishlist = userWishlist,
                    jobId = jobId
                }),
                Headers = CreateCorsHeaders()
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error during wishlist sync: {ex.Message}");
            context.Logger.LogLine($"Stack trace: {ex.StackTrace}");

            // Try to update job status to error if we have a job ID
            if (request.Body != null)
            {
                try
                {
                    var requestData = JsonSerializer.Deserialize<JsonElement>(request.Body);
                    if (requestData.TryGetProperty("jobId", out var jobIdElement))
                    {
                        long jobId = jobIdElement.GetInt64();
                        await _jobRepository.UpdateJobStatusAsync(jobId, "ERROR", $"Sync failed: {ex.Message}");
                    }
                }
                catch
                {
                    // Ignore errors in error handling
                }
            }

            return CreateErrorResponse(500, "Wishlist sync failed", ex.Message);
        }
    }

    private static DateTime? ParseReleaseDate(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return null;

        if (DateTime.TryParse(dateString, out var date))
            return date;

        return null;
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
            { "Access-Control-Allow-Headers", "Content-Type" },
            { "Access-Control-Allow-Methods", "GET, POST, OPTIONS" }
        };
    }
}