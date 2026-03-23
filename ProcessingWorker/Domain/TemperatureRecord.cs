namespace ProcessingWorker.Domain;

public class TemperatureRecord
{
    public int Value { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}