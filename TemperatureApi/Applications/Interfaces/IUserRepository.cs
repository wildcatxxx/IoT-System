using TemperatureApi.Models;

namespace TemperatureApi.Applications.Interfaces;

/// <summary>
/// User repository interface
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Get user by username
    /// </summary>
    Task<User?> GetUserByUsernameAsync(string username);

    /// <summary>
    /// Create a new user
    /// </summary>
    Task<User> CreateUserAsync(User user);

    /// <summary>
    /// Update user's last login timestamp
    /// </summary>
    Task UpdateLastLoginAsync(int userId);
}
