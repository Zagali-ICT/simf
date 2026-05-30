using SIMF.Contracts.PublicRelations;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>D-199 (gap-list event module, Mockup screen 29 / 29b) — public read
/// of the News feed. Returns only active, already-published articles, newest
/// first, paged. Anonymous (matches the public Delegations read).</summary>
public interface IPublicNewsService
{
    Task<PublicNewsPage> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PublicNewsArticle?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
