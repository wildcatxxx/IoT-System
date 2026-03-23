using Microsoft.AspNetCore.Mvc;
using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Models;
using Microsoft.AspNetCore.Authorization;

namespace TemperatureApi.Controllers;

/// <summary>
/// Authentication controller for user registration, login, logout, and token refresh
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(IAuthenticationService authService, ILogger<AuthenticationController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Registration confirmation or error message</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.ConfirmPassword))
            {
                return BadRequest(new { error = "Username, email, password, and password confirmation are required" });
            }

            // Check passwords match
            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(new { error = "Passwords do not match" });
            }

            _logger.LogInformation("Registration attempt for user: {Username}", request.Username);

            var (success, message) = await _authService.RegisterAsync(request.Username, request.Email, request.Password);

            if (!success)
            {
                if (message.Contains("already exists"))
                {
                    return Conflict(new { error = message });
                }
                return BadRequest(new { error = message });
            }

            _logger.LogInformation("Registration successful for user: {Username}", request.Username);
            return Created($"/api/auth/login", new { message = "User registered successfully. Please login with your credentials." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { error = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Login with username and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT access token and refresh token</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { error = "Username and password are required" });
            }

            _logger.LogInformation("Login attempt for user: {Username}", request.Username);

            var result = await _authService.LoginAsync(request.Username, request.Password);

            if (result == null)
            {
                _logger.LogWarning("Login failed for user: {Username}", request.Username);
                return Unauthorized(new { error = "Invalid username or password" });
            }

            _logger.LogInformation("Login successful for user: {Username}", request.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Refresh access token using a valid refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT access token and refresh token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token is required" });
            }

            _logger.LogInformation("Token refresh attempt");

            var result = await _authService.RefreshTokenAsync(request.RefreshToken);

            if (result == null)
            {
                _logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
                return Unauthorized(new { error = "Invalid or expired refresh token" });
            }

            _logger.LogInformation("Token refresh successful");
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { error = "An error occurred during token refresh" });
        }
    }

    /// <summary>
    /// Logout and invalidate the refresh token
    /// </summary>
    /// <param name="request">Refresh token to invalidate</param>
    /// <returns>Logout confirmation</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token is required" });
            }

            _logger.LogInformation("Logout attempt");

            await _authService.LogoutAsync(request.RefreshToken);

            _logger.LogInformation("Logout successful");
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { error = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Validate if a refresh token is still valid
    /// </summary>
    /// <param name="request">Refresh token to validate</param>
    /// <returns>Token validity status</returns>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { error = "Refresh token is required" });
            }

            var isValid = await _authService.IsRefreshTokenValidAsync(request.RefreshToken);

            return Ok(new { isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { error = "An error occurred validating token" });
        }
    }
}
