using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Web.API.Domain.Production;
using Web.API.Domain.Response;
using Web.API.Persistence.Services.Production;

namespace Web.API.Controllers;

[ApiController]
[Route("api/log-buffer")]
public class LogBufferController : ControllerBase
{
    private readonly ILogBufferService _logBufferService;

    public LogBufferController(ILogBufferService logBufferService)
    {
        _logBufferService = logBufferService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaginatedLogBuffers(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pagedResponse = await _logBufferService.GetPaginatedLogBuffersAsync(page, limit, cancellationToken);
            var response = new ApiResponse<PagedResponse<LogBuffer>>
            {
                Success = true,
                StatusCode = 200,
                Data = pagedResponse
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<PagedResponse<LogBuffer>>
            {
                Success = false,
                StatusCode = 500,
                Message = ex.Message
            };
            return StatusCode(500, response);
        }
    }
}
