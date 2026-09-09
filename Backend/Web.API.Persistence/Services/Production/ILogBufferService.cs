using System.Collections.Generic;
using System.Threading.Tasks;
using Web.API.Domain.Production;
using Web.API.Domain.Response;

namespace Web.API.Persistence.Services.Production;

public interface ILogBufferService
{
    Task<PagedResponse<LogBuffer>> GetPaginatedLogBuffersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
