using TemperatureApi.Models;

public interface ITemperatureRepository
{
    Task<IEnumerable<TemperatureDto>> GetPagedAsync(int page, int pageSize);
}