namespace API.Repositories;

using Entities;

public interface IUserRepository
{
    Task<User?> GetUserBySteamIdAsync(string steamId);
    Task<User> CreateUserAsync(User user);
    Task<User> UpdateUserAsync(User user);
    Task<User> UpsertUserAsync(User user);
}
