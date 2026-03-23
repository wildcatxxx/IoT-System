namespace ProcessingWorker.Config;

public class MqttSettings
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string Topic { get; set; } = "factory/temperature";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}