using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SHMS.Storage;

namespace Web.API.Controllers;

[ApiController]
[Authorize]
[Route("api/shms-system/storage")]
public sealed class StorageSettingsController(DataRetentionStore store) : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken token) => ApiOk(await store.GetSettingsAsync(token));

    [HttpPut("settings")]
    public async Task<IActionResult> Save([FromBody] RetentionSettings settings, CancellationToken token) =>
        ApiOk(await store.SaveSettingsAsync(settings, token), "Storage settings saved.");

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] RetentionSettings settings, CancellationToken token) =>
        ApiOk(await store.PreviewAsync(settings, token));

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken token) => ApiOk(await store.GetStatusAsync(token));
}
