using SIMF.Contracts.PublicRelations;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>Public read of the News feed.
/// Returns only active, already-published articles, newest
/// first, paged. Anonymous (matches the public Delegations read).</summary>
public interface IPublicNewsService
{
    Task<PublicNewsPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PublicNewsArticle?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
