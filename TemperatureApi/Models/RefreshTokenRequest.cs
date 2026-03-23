namespace TemperatureApi.Models;

/// <summary>
/// Refresh token request model
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// The refresh token received from login
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
}
