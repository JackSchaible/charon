namespace API.Repositories;

using Entities;
using Dapper;
using Microsoft.Data.SqlClient;

public interface IUserRepository
{
    Task<User?> GetUserBySteamIdAsync(string steamId);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<User> UpsertUserAsync(User user);
}

public class UserRepository(string connectionString) : IUserRepository
{
    private readonly string _connectionString =
        connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    private const string GetUserSql = """
                                      SELECT Id, SteamId, CreatedAt, AvatarUrl, ProfileUrl, Username
                                      FROM Users 
                                      WHERE SteamId = @SteamId
                                      """;
    private const string CreateUserSql = """
                                         INSERT INTO Users (SteamId, CreatedAt, AvatarUrl, ProfileUrl, Username)
                                         OUTPUT INSERTED.Id, INSERTED.SteamId, INSERTED.CreatedAt, INSERTED.AvatarUrl, INSERTED.ProfileUrl, INSERTED.Username
                                         VALUES (@SteamId, @CreatedAt, @AvatarUrl, @ProfileUrl, @Username)
                                         """;

    private const string UpdateUserSql = """
                                         UPDATE Users 
                                         SET AvatarUrl = @AvatarUrl, 
                                             ProfileUrl = @ProfileUrl, 
                                             Username = @Username
                                         OUTPUT INSERTED.Id, INSERTED.SteamId, INSERTED.CreatedAt, INSERTED.AvatarUrl, INSERTED.ProfileUrl, INSERTED.Username
                                         WHERE Id = @Id
                                         """;

    private const string GetUserByIdSql = """
                                         SELECT Id, SteamId, CreatedAt, AvatarUrl, ProfileUrl, Username
                                         FROM Users 
                                         WHERE Id = @Id
                                         """;

    public async Task<User?> GetUserBySteamIdAsync(string steamId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<User>(GetUserSql, new { SteamId = steamId });
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<User>(GetUserByIdSql, new { Id = id });
    }

    public async Task<User> CreateUserAsync(User user)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        User insertedUser = await connection.QuerySingleAsync<User>(CreateUserSql, user);
        return insertedUser;
    }

    public async Task<User> UpdateUserAsync(User user)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        User updatedUser = await connection.QuerySingleAsync<User>(UpdateUserSql, user);
        return updatedUser;
    }

    public async Task<User> UpsertUserAsync(User user)
    {
        // First try to get existing user
        User? existingUser = await GetUserBySteamIdAsync(user.SteamId!);

        if (existingUser != null)
        {
            // Update existing user with new information
            existingUser.Username = user.Username;
            existingUser.AvatarUrl = user.AvatarUrl;
            existingUser.ProfileUrl = user.ProfileUrl;

            return await UpdateUserAsync(existingUser);
        }
        else
        {
            // Create new user
            user.CreatedAt = DateTime.UtcNow;
            return await CreateUserAsync(user);
        }
    }
}