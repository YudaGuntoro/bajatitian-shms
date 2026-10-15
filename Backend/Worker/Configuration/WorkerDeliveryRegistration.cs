using Worker.Application.Handlers;
using Worker.Infrastructure.Http;
using Worker.Infrastructure.Persistence;
using Worker.Infrastructure.Redis;

namespace Worker.Configuration;

public static class WorkerDeliveryRegistration
{
    public static IServiceCollection AddWorkerDelivery(this IServiceCollection services, WorkerRuntimeOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ILogWriterService, LogWriterService>();
        services.AddSingleton<ISensorReadingWriterService, SensorReadingWriterService>();
        services.AddSingleton<IShmsSensorHandler, ShmsSensorHandler>();
        if (options.LocalOnly)
        {
            // Do not construct Redis/HTTP services at all during local testing.
            services.AddSingleton<IMqttMessageHandler, LocalMqttMessageHandler>();
        }
        else
        {
            services.AddSingleton<ISyncStatusWriterService, SyncStatusWriterService>();
            services.AddSingleton<IMainServerUploader, MainServerUploader>();
            services.AddSingleton<IRedisMqttMessageBuffer, RedisMqttMessageBuffer>();
            services.AddSingleton<IMqttMessageHandler, MqttMessageRouter>();
        }
        return services;
    }
}
