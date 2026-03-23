using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Models;

namespace TemperatureApi.Applications.Services;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly int _accessTokenExpirationMinutes = 15;
    private readonly int _refreshTokenExpirationDays = 7;

    public AuthenticationService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthenticationService> logger,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    public async Task<(bool Success, string Message)> RegisterAsync(string username, string email, string password)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Username, email, and password are required");
            }

            // Validate username (alphanumeric and underscore, 3-20 characters)
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]{3,20}$"))
            {
                return (false, "Username must be 3-20 characters and contain only letters, numbers, and underscores");
            }

            // Validate email
            if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                return (false, "Invalid email format");
            }

            // Validate password (at least 8 chars, 1 uppercase, 1 lowercase, 1 number, 1 special char)
            if (!Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$"))
            {
                return (false, "Password must be at least 8 characters and contain uppercase, lowercase, number, and special character");
            }

            // Check if user already exists
            var existingUser = await _userRepository.GetUserByUsernameAsync(username);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: Username already exists - {Username}", username);
                return (false, "Username already exists");
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            // Create user
            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createdUser = await _userRepository.CreateUserAsync(user);

            _logger.LogInformation("User registered successfully: {Username}", username);
            return (true, "Registration successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user: {Username}", username);
            return (false, "An error occurred during registration");
        }
    }

    /// <summary>
    /// Authenticate user and return JWT tokens
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(string username, string password)
    {
        try
        {
            // Get user from database
            var user = await _userRepository.GetUserByUsernameAsync(username);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found - {Username}", username);
                return null;
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: User inactive - {Username}", username);
                return null;
            }

            // Verify password
            if (!VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password - {Username}", username);
                return null;
            }

            // Update last login
            await _userRepository.UpdateLastLoginAsync(user.Id);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();
            var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

            // Store refresh token
            await _refreshTokenRepository.StoreRefreshTokenAsync(user.Id, refreshToken, refreshTokenExpiry);

            _logger.LogInformation("Login successful for user: {Username}", username);

            return new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = _accessTokenExpirationMinutes * 60,
                TokenType = "Bearer",
                Username = user.Username
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user: {Username}", username);
            throw;
        }
    }

    /// <summary>
    /// Refresh an expired access token using a refresh token
    /// </summary>
    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Token refresh attempt");

            // Validate refresh token
            if (!await _refreshTokenRepository.IsRefreshTokenValidAsync(refreshToken))
            {
                _logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
                return null;
            }

            // Get token details
            var tokenDetails = await _refreshTokenRepository.GetRefreshTokenAsync(refreshToken);
            if (tokenDetails == null)
            {
                _logger.LogWarning("Token refresh failed: Refresh token not found");
                return null;
            }

            var (userId, _) = tokenDetails.Value;

            // Get user (you'll need to add this method to IUserRepository)
            // For now, we'll need to fetch the user by ID
            // This is a simplified version - you may need to extend IUserRepository
            _logger.LogInformation("Token refresh successful for user ID: {UserId}", userId);

            // Generate new access token (would need user details)
            // For now, return a placeholder - this needs to be expanded
            var newAccessToken = GenerateAccessTokenForUserId(userId);
            var newRefreshToken = GenerateRefreshToken();
            var newRefreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);

            // Invalidate old refresh token
            await _refreshTokenRepository.InvalidateRefreshTokenAsync(refreshToken);

            // Store new refresh token
            await _refreshTokenRepository.StoreRefreshTokenAsync(userId, newRefreshToken, newRefreshTokenExpiry);

            return new LoginResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                ExpiresIn = _accessTokenExpirationMinutes * 60,
                TokenType = "Bearer"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            throw;
        }
    }

    /// <summary>
    /// Invalidate a refresh token (logout)
    /// </summary>
    public async Task LogoutAsync(string refreshToken)
    {
        try
        {
            _logger.LogInformation("Logout attempt");

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _refreshTokenRepository.InvalidateRefreshTokenAsync(refreshToken);
                _logger.LogInformation("Logout successful");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    /// <summary>
    /// Validate if a refresh token is valid
    /// </summary>
    public async Task<bool> IsRefreshTokenValidAsync(string refreshToken)
    {
        return await _refreshTokenRepository.IsRefreshTokenValidAsync(refreshToken);
    }

    /// <summary>
    /// Generate JWT access token
    /// </summary>
    private string GenerateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("sub", user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate JWT access token for user ID
    /// </summary>
    private string GenerateAccessTokenForUserId(int userId)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generate refresh token
    /// </summary>
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }

    /// <summary>
    /// Verify password against hash
    /// </summary>
    private bool VerifyPassword(string password, string hash)
    {
        // Using BCrypt or similar library would be better in production
        // For demonstration, we'll use a simple approach
        // In production, use: BCrypt.Net-Core or similar
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
