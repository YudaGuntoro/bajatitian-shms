using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web.API.Domain.Auth;
using Web.API.Persistence.Services.AuthService;

namespace Web.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "ADMIN")]
public class UsersController : ApiControllerBase
{
    private readonly IUserManagementService _service;

    public UsersController(IUserManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Users(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetUsersAsync(page, limit, search, isActive, cancellationToken);

        return Ok(new
        {
            success = true,
            statusCode = StatusCodes.Status200OK,
            message = "Data retrieved successfully",
            data = result.Users,
            pagination = new
            {
                page = result.Page,
                limit = result.Limit,
                total = result.Total,
                totalPage = result.TotalPage
            }
        });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken)
    {
        return ApiOk(await _service.GetRolesAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _service.GetUserAsync(id, cancellationToken);
        return user is null
            ? ApiNotFound("User was not found.")
            : ApiOk(user);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return ApiCreated(
                await _service.CreateUserAsync(request, cancellationToken),
                "User created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UpdateUserAsync(id, request, cancellationToken);
            return user is null
                ? ApiNotFound("User was not found.")
                : ApiOk(user, "User updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.DeactivateUserAsync(id, cancellationToken);
            return user is null
                ? ApiNotFound("User was not found.")
                : ApiOk(user, "User deactivated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }
}
