namespace TemperatureApi.Models;

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
