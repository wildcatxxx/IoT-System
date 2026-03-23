using ProcessingWorker.Infrastructure.Messaging;

namespace ProcessingWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MqttConsumer _consumer;

    public Worker(ILogger<Worker> logger, MqttConsumer consumer)
    {
        _consumer = consumer;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);
        try
        {
            await _consumer.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT consumer");
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // simple periodic heartbeat while the host is running
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker heartbeat: {time}", DateTimeOffset.Now);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // expected during shutdown — no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in ExecuteAsync");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
        try
        {
            // attempt graceful shutdown of the consumer if supported
            if (_consumer is IAsyncDisposable || _consumer is IDisposable)
            {
                // prefer an explicit stop method if available
                var stopMethod = _consumer.GetType().GetMethod("StopAsync") ??
                                 _consumer.GetType().GetMethod("Stop");
                if (stopMethod != null)
                {
                    var result = stopMethod.Invoke(_consumer, Array.Empty<object>());
                    if (result is Task t) await t;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop MQTT consumer cleanly");
        }

        await base.StopAsync(cancellationToken);
    }
}
