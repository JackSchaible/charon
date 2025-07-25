namespace API.Repositories;

using Entities;
using Dapper;
using Microsoft.Data.SqlClient;

public interface IJobRepository
{
    Task<Job?> GetJobByIdAsync(long jobId);
    Task UpdateJobStatusAsync(long jobId, string statusCode, string? details = null);
    Task<Job> CreateJobAsync(int userId);
}

public class JobRepository(string connectionString) : IJobRepository
{
    private readonly string _connectionString =
        connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    private const string CreateJobSql = """
        INSERT INTO Job (UserId, Type, StatusId, CreatedAt, UpdatedAt, Details)
        OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.Type, INSERTED.StatusId, INSERTED.CreatedAt, INSERTED.UpdatedAt
        VALUES (@UserId, @Type, (select Id from JobStatus where Code = 'NEW'), @CreatedAt, @UpdatedAt, @Details)
        """;

    private const string GetJobSql = """
        SELECT Id, UserId, Type, StatusId, CreatedAt, UpdatedAt, Details
        FROM Job 
        WHERE Id = @JobId
        """;

    private const string UpdateJobStatusSql = """
        UPDATE Job 
        SET StatusId = (SELECT Id FROM JobStatus WHERE Code = @StatusCode),
            UpdatedAt = @UpdatedAt,
            Details = @Details
        WHERE Id = @JobId
        """;

    public async Task<Job> CreateJobAsync(int userId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<Job>(CreateJobSql, new
        {
            UserId = userId,
            Type = "SYNC_STEAM",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Details = "Syncing user's wishlist with Steam"
        });
    }

    public async Task<Job?> GetJobByIdAsync(long jobId)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<Job>(GetJobSql, new { JobId = jobId });
    }

    public async Task UpdateJobStatusAsync(long jobId, string statusCode, string? details = null)
    {
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(UpdateJobStatusSql, new
        {
            JobId = jobId,
            StatusCode = statusCode,
            UpdatedAt = DateTime.UtcNow,
            Details = details
        });
    }
}