namespace SIMF.Application.Programme.Abstractions;

/// <summary>
/// Session favourites (المفضلة) — the per-user heart toggle on the
/// session-summaries (1388:8392) + my-sessions (1388:9067) screens. Add/remove
/// are idempotent; the list returns the caller's favourited session ids so the
/// client can mark hearts + drive the المفضلة filter. Approved-account only.
/// </summary>
public interface ISessionFavouriteService
{
    Task AddAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListMineAsync(Guid userId, CancellationToken cancellationToken = default);
}
