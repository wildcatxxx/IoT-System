using System.Text;
using MQTTnet;
using MQTTnet.Client;
// using MQTTnet.Client.Options;
using Microsoft.Extensions.Options;
using ProcessingWorker.Config;
using ProcessingWorker.Application.Services;

namespace ProcessingWorker.Infrastructure.Messaging;

public class MqttConsumer
{
    private readonly TemperatureService _service;
    private readonly MqttSettings _settings;
    private IMqttClient _client;

    public MqttConsumer(TemperatureService service, IOptions<MqttSettings> options)
    {
        _service = service;
        _settings = options.Value;
    }

    public async Task StartAsync()
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.BrokerHost, _settings.BrokerPort)
            .WithCredentials(_settings.Username, _settings.Password)
            .Build();

        await _client.ConnectAsync(options);

        await _client.SubscribeAsync(_settings.Topic);

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            var value = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            Console.WriteLine($"Received: {value}");

            await _service.ProcessAsync(value);
        };
    }
}