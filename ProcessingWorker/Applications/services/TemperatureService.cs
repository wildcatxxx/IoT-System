using ProcessingWorker.Domain;
using ProcessingWorker.Infrastructure.Persistence;

namespace ProcessingWorker.Application.Services;

public class TemperatureService
{
    private readonly TemperatureRepository _repository;

    public TemperatureService(TemperatureRepository repository)
    {
        _repository = repository;
    }

    public async Task ProcessAsync(string message)
    {

        if (!int.TryParse(message, out var value))
            throw new Exception("Invalid temperature value");

        var record = new TemperatureRecord
        {
            Value = value
        };

        await _repository.SaveAsync(record);
    }
}