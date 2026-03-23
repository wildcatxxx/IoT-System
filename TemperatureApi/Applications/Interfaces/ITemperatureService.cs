using TemperatureApi.Models;

namespace TemperatureApi.Applications.Interfaces;

public interface ITemperatureService
{
    Task<IEnumerable<TemperatureDto>> GetLatestTemperaturesAsync(int page, int pageSize);
}