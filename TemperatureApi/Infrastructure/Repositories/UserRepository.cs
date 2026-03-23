using Dapper;
using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Models;
using System.Data;
using Polly;
using Polly.Retry;
using TemperatureApi.Infrastructure.Policies;

namespace TemperatureApi.Infrastructure.Repositories;

/// <summary>
/// User repository implementation
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IDbConnection _db;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IDbConnection db, AsyncRetryPolicy retryPolicy, ILogger<UserRepository> logger)
    {
        _db = db;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Get user by username
    /// </summary>
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        try
        {
            var sql = @"
                SELECT id, username, email, password_hash AS PasswordHash, 
                       created_at AS CreatedAt, last_login AS LastLogin, is_active AS IsActive
                FROM users
                WHERE username = @Username AND is_active = true";

            var user = await _retryPolicy.ExecuteAsync(async () =>
                await _db.QueryFirstOrDefaultAsync<User>(sql, new { Username = username })
            );

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user by username: {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    public async Task<User> CreateUserAsync(User user)
    {
        try
        {
            var sql = @"
                INSERT INTO users (username, email, password_hash, created_at, is_active)
                VALUES (@Username, @Email, @PasswordHash, @CreatedAt, @IsActive)
                RETURNING id, username, email, password_hash AS PasswordHash, 
                          created_at AS CreatedAt, is_active AS IsActive";

            user.CreatedAt = DateTime.UtcNow;
            user.IsActive = true;

            var createdUser = await _retryPolicy.ExecuteAsync(async () =>
                await _db.QueryFirstOrDefaultAsync<User>(sql, user)
            );

            _logger.LogInformation("User created: {Username}", user.Username);
            return createdUser ?? user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {Username}", user.Username);
            throw;
        }
    }

    /// <summary>
    /// Update user's last login timestamp
    /// </summary>
    public async Task UpdateLastLoginAsync(int userId)
    {
        try
        {
            var sql = @"
                UPDATE users
                SET last_login = @LastLogin
                WHERE id = @UserId";

            await _retryPolicy.ExecuteAsync(async () =>
                await _db.ExecuteAsync(sql, new { LastLogin = DateTime.UtcNow, UserId = userId })
            );

            _logger.LogInformation("Last login updated for user ID: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last login for user ID: {UserId}", userId);
            throw;
        }
    }
}
