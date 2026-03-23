using TemperatureApi.Models;

namespace TemperatureApi.Applications.Interfaces;

/// <summary>
/// Authentication service interface
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Register a new user
    /// </summary>
    Task<(bool Success, string Message)> RegisterAsync(string username, string email, string password);

    /// <summary>
    /// Authenticate user and return JWT tokens
    /// </summary>
    Task<LoginResponse?> LoginAsync(string username, string password);

    /// <summary>
    /// Refresh an expired access token using a refresh token
    /// </summary>
    Task<LoginResponse?> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Invalidate a refresh token (logout)
    /// </summary>
    Task LogoutAsync(string refreshToken);

    /// <summary>
    /// Validate if a refresh token is valid
    /// </summary>
    Task<bool> IsRefreshTokenValidAsync(string refreshToken);
}
