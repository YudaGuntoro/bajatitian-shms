using Web.API.Domain.Production;

namespace Web.API.Persistence.Services.Production;

public interface IMqttConfigurationService
{
    Task<MqttConfigurationResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<MqttConfigurationResponse> UpdateAsync(
        UpdateMqttConfigurationRequest request,
        CancellationToken cancellationToken = default);
}
