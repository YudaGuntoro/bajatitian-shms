using Microsoft.AspNetCore.Mvc;
using System.Net.Sockets;
using Web.API.Domain.Production;
using Web.API.Persistence.Services.Production;

namespace Web.API.Controllers;

[ApiController]
[Route("api/shms-system")]
public class SHMSSystemController : ApiControllerBase
{
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IMqttConfigurationService _mqttConfigurationService;
    private readonly IShmsStatusService _shmsStatusService;

    public SHMSSystemController(
        ISystemSettingsService systemSettingsService,
        IMqttConfigurationService mqttConfigurationService,
        IShmsStatusService shmsStatusService)
    {
        _systemSettingsService = systemSettingsService;
        _mqttConfigurationService = mqttConfigurationService;
        _shmsStatusService = shmsStatusService;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await _systemSettingsService.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await _systemSettingsService.UpdateAsync(request, cancellationToken), "Settings updated successfully");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("mqtt-configuration")]
    public async Task<IActionResult> MqttConfiguration(CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await _mqttConfigurationService.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("mqtt-configuration")]
    public async Task<IActionResult> UpdateMqttConfiguration(
        [FromBody] UpdateMqttConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await _mqttConfigurationService.UpdateAsync(request, cancellationToken), "MQTT configuration updated successfully");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        try
        {
            return ApiOk(await _shmsStatusService.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("sensors/latest")]
    public async Task<IActionResult> LatestSensorReadings(CancellationToken cancellationToken)
    {
        return ApiOk(await _shmsStatusService.GetLatestReadingsAsync(cancellationToken));
    }

    [HttpGet("mqtt-broker/status")]
    public async Task<IActionResult> MqttBrokerStatus(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await _mqttConfigurationService.GetAsync(cancellationToken);
            var port = int.TryParse(settings.BrokerPort, out var parsedPort) ? parsedPort : 1883;
            var isOnline = await CheckTcpReachableAsync(settings.BrokerHost, port, cancellationToken);

            return ApiOk(new
            {
                host = settings.BrokerHost,
                port,
                configured = true,
                online = isOnline,
                use_tls = settings.UseTls,
                checked_at = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    private static async Task<bool> CheckTcpReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(host, port, cancellationToken).AsTask();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var completed = await Task.WhenAny(connectTask, timeoutTask);
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
