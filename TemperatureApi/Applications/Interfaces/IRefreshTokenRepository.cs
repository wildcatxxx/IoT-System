namespace TemperatureApi.Applications.Interfaces;

/// <summary>
/// Refresh token repository interface
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Store a refresh token
    /// </summary>
    Task StoreRefreshTokenAsync(int userId, string token, DateTime expiresAt);

    /// <summary>
    /// Get refresh token details
    /// </summary>
    Task<(int UserId, DateTime ExpiresAt)?> GetRefreshTokenAsync(string token);

    /// <summary>
    /// Invalidate a refresh token
    /// </summary>
    Task InvalidateRefreshTokenAsync(string token);

    /// <summary>
    /// Check if a refresh token is valid and not expired
    /// </summary>
    Task<bool> IsRefreshTokenValidAsync(string token);
}
