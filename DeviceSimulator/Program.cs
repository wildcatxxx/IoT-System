// See https://aka.ms/new-console-template for more information
using MQTTnet;
using MQTTnet.Client;

var factory = new MqttFactory();
var client = factory.CreateMqttClient();

await client.ConnectAsync(new MqttClientOptionsBuilder()
    .WithTcpServer("localhost", 1883)
    .Build());

while (true)
{
    var temp = Random.Shared.Next(20, 40);

    var message = new MqttApplicationMessageBuilder()
        .WithTopic("factory/temperature")
        .WithPayload(temp.ToString())
        .Build();

    await client.PublishAsync(message);

    Console.WriteLine($"Sent: {temp}");
    await Task.Delay(5000);
}