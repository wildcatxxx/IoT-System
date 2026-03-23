namespace TemperatureApi.Models;

/// <summary>
/// User registration request model
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Password confirmation
    /// </summary>
    public string ConfirmPassword { get; set; } = string.Empty;
}
