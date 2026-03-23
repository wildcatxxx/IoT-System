using Dapper;
using TemperatureApi.Applications.Interfaces;
using System.Data;
using Polly;
using Polly.Retry;
using TemperatureApi.Infrastructure.Policies;

namespace TemperatureApi.Infrastructure.Repositories;

/// <summary>
/// Refresh token repository implementation
/// </summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnection _db;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly ILogger<RefreshTokenRepository> _logger;

    public RefreshTokenRepository(IDbConnection db, AsyncRetryPolicy retryPolicy, ILogger<RefreshTokenRepository> logger)
    {
        _db = db;
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    /// <summary>
    /// Store a refresh token
    /// </summary>
    public async Task StoreRefreshTokenAsync(int userId, string token, DateTime expiresAt)
    {
        try
        {
            var sql = @"
                INSERT INTO refresh_tokens (user_id, token, expires_at, is_valid, created_at)
                VALUES (@UserId, @Token, @ExpiresAt, true, @CreatedAt)";

            await _retryPolicy.ExecuteAsync(async () =>
                await _db.ExecuteAsync(sql, new 
                { 
                    UserId = userId, 
                    Token = token, 
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow
                })
            );

            _logger.LogInformation("Refresh token stored for user ID: {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing refresh token for user ID: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Get refresh token details
    /// </summary>
    public async Task<(int UserId, DateTime ExpiresAt)?> GetRefreshTokenAsync(string token)
    {
        try
        {
            var sql = @"
                SELECT user_id AS UserId, expires_at AS ExpiresAt
                FROM refresh_tokens
                WHERE token = @Token AND is_valid = true AND expires_at > @Now";

            var result = await _retryPolicy.ExecuteAsync(async () =>
                await _db.QueryFirstOrDefaultAsync<(int UserId, DateTime ExpiresAt)>(
                    sql, 
                    new { Token = token, Now = DateTime.UtcNow }
                )
            );

            return result != default ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting refresh token");
            throw;
        }
    }

    /// <summary>
    /// Invalidate a refresh token
    /// </summary>
    public async Task InvalidateRefreshTokenAsync(string token)
    {
        try
        {
            var sql = @"
                UPDATE refresh_tokens
                SET is_valid = false
                WHERE token = @Token";

            await _retryPolicy.ExecuteAsync(async () =>
                await _db.ExecuteAsync(sql, new { Token = token })
            );

            _logger.LogInformation("Refresh token invalidated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating refresh token");
            throw;
        }
    }

    /// <summary>
    /// Check if a refresh token is valid and not expired
    /// </summary>
    public async Task<bool> IsRefreshTokenValidAsync(string token)
    {
        try
        {
            var sql = @"
                SELECT COUNT(1)
                FROM refresh_tokens
                WHERE token = @Token AND is_valid = true AND expires_at > @Now";

            var count = await _retryPolicy.ExecuteAsync(async () =>
                await _db.ExecuteScalarAsync<int>(sql, new { Token = token, Now = DateTime.UtcNow })
            );

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking refresh token validity");
            throw;
        }
    }
}
