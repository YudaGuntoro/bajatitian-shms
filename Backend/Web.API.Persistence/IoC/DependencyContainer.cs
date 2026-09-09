using Microsoft.Extensions.DependencyInjection;
using Web.API.Persistence.Repositories.Production;
using Web.API.Persistence.Services.AuthService;
using Web.API.Persistence.Services.Production;

namespace Web.API.Persistence.IoC;

public static class DependencyContainer
{
    public static void AddIoCService(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILogBufferRepository, LogBufferRepository>();
        services.AddScoped<ILogBufferService, LogBufferService>();
        services.AddScoped<IMqttConfigurationRepository, MqttConfigurationRepository>();
        services.AddScoped<IMqttConfigurationService, MqttConfigurationService>();
    }
}
