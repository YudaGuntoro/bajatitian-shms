using System.Threading.Tasks;
using Web.API.Domain.Production;
using Web.API.Domain.Response;
using Web.API.Persistence.Repositories.Production;

namespace Web.API.Persistence.Services.Production;

public class LogBufferService : ILogBufferService
{
    private readonly ILogBufferRepository _repository;

    public LogBufferService(ILogBufferRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<LogBuffer>> GetPaginatedLogBuffersAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var totalRecords = await _repository.CountAsync(cancellationToken);
        var items = await _repository.GetPageAsync(pageNumber, pageSize, cancellationToken);

        return new PagedResponse<LogBuffer>
        {
            Data = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (totalRecords + pageSize - 1) / pageSize
        };
    }
}
