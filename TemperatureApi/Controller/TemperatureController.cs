using Microsoft.AspNetCore.Mvc;
using TemperatureApi.Applications.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace TemperatureApi.Controllers;

[EnableRateLimiting("fixed")]
[Authorize]
[ApiController]
[Route("api/temps")]
public class TemperatureController : ControllerBase
{
    private readonly ITemperatureService _service;
    private readonly ILogger<TemperatureController> _logger;

    public TemperatureController(ITemperatureService service, ILogger<TemperatureController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(int page = 1, int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("GET /api/temps called with page={Page}, pageSize={PageSize}", page, pageSize);
            
            if (page < 1 || pageSize < 1 || pageSize > 100)
                return BadRequest(new { error = "Page must be >= 1, pageSize must be between 1-100" });

            var result = await _service.GetLatestTemperaturesAsync(page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching temperatures");
            return StatusCode(500, new { error = "An error occurred while fetching temperatures" });
        }
    }
}