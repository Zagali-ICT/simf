using SIMF.Contracts.Archive;

namespace SIMF.Application.Archive.Abstractions;

/// <summary>D-199 — read-only public projection of active archive editions
/// for the Past Editions screen (Mockup screen 24). The implementation also
/// honours the archive-visibility operations toggle (D-166): when the toggle
/// is off it returns an empty payload.</summary>
public interface IPublicArchiveService
{
    Task<PublicArchive> ListAsync(CancellationToken cancellationToken = default);
}
