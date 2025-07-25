namespace API.Repositories;

using Entities;
using Dapper;
using Microsoft.Data.SqlClient;

public interface IWishlistRepository
{
    Task<List<WishlistItem>> GetUserWishlistAsync(int userId);
    Task UpsertWishlistItemAsync(WishlistItem item);
    Task ClearUserWishlistAsync(int userId);
}

public class WishlistRepository(string connectionString) : IWishlistRepository
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private const string GetUserWishlistSql = """
        SELECT w.UserId, w.GameId, w.Notes, w.AddedAt, g.AppId, g.Title, g.ImageUrl, g.Price, g.ReleaseDate, g.LastFetched
        FROM WishlistItem w
        LEFT JOIN Game g ON w.GameId = g.AppId
        WHERE w.UserId = @UserId
        """;

    private const string UpsertWishlistItemSql = """
        MERGE WishlistItem AS target
        USING (VALUES (@UserId, @GameId, @Notes, @AddedAt)) AS source (UserId, GameId, Notes, AddedAt)
        ON target.UserId = source.UserId AND target.GameId = source.GameId
        WHEN MATCHED THEN
            UPDATE SET Notes = source.Notes, AddedAt = source.AddedAt
        WHEN NOT MATCHED THEN
            INSERT (UserId, GameId, Notes, AddedAt)
            VALUES (source.UserId, source.GameId, source.Notes, source.AddedAt);
        """;

    private const string ClearUserWishlistSql = """
        DELETE FROM WishlistItem WHERE UserId = @UserId
        """;

    public async Task<List<WishlistItem>> GetUserWishlistAsync(int userId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        var result = await connection.QueryAsync<WishlistItem>(GetUserWishlistSql, new { UserId = userId });
        return result.ToList();
    }

    public async Task UpsertWishlistItemAsync(WishlistItem item)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(UpsertWishlistItemSql, item);
    }

    public async Task ClearUserWishlistAsync(int userId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(ClearUserWishlistSql, new { UserId = userId });
    }
}
