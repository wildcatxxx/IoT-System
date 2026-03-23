using ProcessingWorker;
using ProcessingWorker.Infrastructure.Messaging;
using ProcessingWorker.Infrastructure.Persistence;
using ProcessingWorker.Application.Services;
using ProcessingWorker.Config;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttConsumer>(builder.Configuration.GetSection("Mqtt"));

builder.Services.AddSingleton<TemperatureRepository>();
builder.Services.AddSingleton<TemperatureService>();
builder.Services.AddSingleton<MqttConsumer>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
