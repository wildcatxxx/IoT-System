using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace TemperatureApi.Applications.Services;

public class TemperatureService : ITemperatureService
{
    private readonly ITemperatureRepository _repository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TemperatureService> _logger;
    private const int CacheExpirationSeconds = 30;

    public TemperatureService(
        ITemperatureRepository repository,
        IDistributedCache cache,
        ILogger<TemperatureService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<TemperatureDto>> GetLatestTemperaturesAsync(int page, int pageSize)
    {
        var cacheKey = $"temps_{page}_{pageSize}";
        
        try
        {
            var cached = await _cache.GetStringAsync(cacheKey);

            if (cached != null)
            {
                _logger.LogInformation("Cache HIT for temperatures page {Page}", page);
                return JsonSerializer.Deserialize<IEnumerable<TemperatureDto>>(cached)!;
            }

            _logger.LogInformation("Cache MISS for temperatures page {Page}, fetching from repository", page);
            var data = await _repository.GetPagedAsync(page, pageSize);

            await _cache.SetStringAsync(cacheKey,
                JsonSerializer.Serialize(data),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheExpirationSeconds)
                });

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching temperatures for page {Page}", page);
            throw;
        }
    }
}