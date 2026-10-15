using Worker.Infrastructure.Logging;
using Worker.Application.Handlers;
using Worker.Infrastructure.Mqtt;
using Worker.Infrastructure.Persistence;
using SHMS.Storage;
using Worker.Configuration;

var builder = Host.CreateApplicationBuilder(args);
var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Worker";
});

builder.Logging.AddProvider(new FileLoggerProvider(logDirectory));

builder.Services.AddSingleton<MqttTopicConfigService>();
builder.Services.AddWorkerDelivery(WorkerRuntimeOptions.Load());
builder.Services.AddSingleton<MqttClientService>();
builder.Services.AddSingleton<IMqttClientService>(serviceProvider => serviceProvider.GetRequiredService<MqttClientService>());
builder.Services.AddSingleton<IMqttPublisher>(serviceProvider => serviceProvider.GetRequiredService<MqttClientService>());
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<MqttClientService>());
builder.Services.AddSingleton(_ => new DataRetentionStore(DatabaseConfig.MysqlConnString));
builder.Services.AddHostedService<DataRetentionWorker>();

var host = builder.Build();
host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Worker.Startup")
    .LogInformation("SHMS Worker starting. LogDirectory={LogDirectory}", logDirectory);

// Schema changes are applied through SQL migrations, never while ingesting readings.
await host.Services.GetRequiredService<ISensorReadingWriterService>().WaitUntilReadyAsync();
await host.Services.GetRequiredService<IMqttMessageHandler>().WaitUntilReadyAsync();
await host.RunAsync();
