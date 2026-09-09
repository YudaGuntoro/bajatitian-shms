using Web.API.Domain.Production;

namespace Web.API.Persistence.Repositories.Production;

public interface ILogBufferRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LogBuffer>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
