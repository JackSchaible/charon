namespace API.Repositories;

using Entities;
using Dapper;
using Microsoft.Data.SqlClient;

public interface IGameRepository
{
    Task<Game?> GetGameByAppIdAsync(string appId);
    Task<Game> UpsertGameAsync(Game game);
    Task<bool> GameExistsAsync(string appId);
}

public class GameRepository(string connectionString) : IGameRepository
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private const string GetGameSql = """
        SELECT AppId, Title, ImageUrl, Price, ReleaseDate, LastFetched
        FROM Game 
        WHERE AppId = @AppId
        """;

    private const string GameExistsSql = """
        SELECT CASE WHEN EXISTS(SELECT 1 FROM Game WHERE AppId = @AppId) THEN 1 ELSE 0 END
        """;

    private const string InsertGameSql = """
        INSERT INTO Game (AppId, Title, ImageUrl, Price, ReleaseDate, LastFetched)
        VALUES (@AppId, @Title, @ImageUrl, @Price, @ReleaseDate, @LastFetched)
        """;

    private const string UpdateGameSql = """
        UPDATE Game 
        SET Title = @Title, 
            ImageUrl = @ImageUrl, 
            Price = @Price, 
            ReleaseDate = @ReleaseDate,
            LastFetched = @LastFetched
        WHERE AppId = @AppId
        """;

    public async Task<Game?> GetGameByAppIdAsync(string appId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Game>(GetGameSql, new { AppId = appId });
    }

    public async Task<bool> GameExistsAsync(string appId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<bool>(GameExistsSql, new { AppId = appId });
    }

    public async Task<Game> UpsertGameAsync(Game game)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);

        var existingGame = await GetGameByAppIdAsync(game.AppId!);

        if (existingGame != null)
        {
            // Update existing game
            await connection.ExecuteAsync(UpdateGameSql, game);
        }
        else
        {
            // Insert new game
            await connection.ExecuteAsync(InsertGameSql, game);
        }

        return game;
    }
}
