namespace TemperatureApi.Models;

/// <summary>
/// User model
/// </summary>
public class User
{
    /// <summary>
    /// User ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// User creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>
    /// Whether the user is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
