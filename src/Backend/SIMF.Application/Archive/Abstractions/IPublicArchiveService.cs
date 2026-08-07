using SIMF.Contracts.Archive;

namespace SIMF.Application.Archive.Abstractions;

/// <summary>Read-only public projection of active archive editions
/// for the Past Editions screen. The implementation also
/// honours the archive-visibility operations toggle: when the toggle
/// is off it returns an empty payload.</summary>
public interface IPublicArchiveService
{
    Task<PublicArchive> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The "تفاصيل النسخة" screen — public detail for one
    /// past edition; null when the archive is hidden (toggle off) or the edition
    /// is missing / inactive.</summary>
    Task<PublicArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);
}
