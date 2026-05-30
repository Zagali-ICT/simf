// Tests: SIMF.Api.Tests/ArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Operations.Abstractions;
using SIMF.Contracts.Archive;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Archive;

/// <summary>D-199 — read-only public projection of active archive editions,
/// ordered newest-first for the Past Editions screen (Mockup screen 24).
/// Gated by the archive-visibility operations toggle (D-166): when the toggle
/// is off the payload is empty, so the App / Website hide the screen without
/// a separate visibility round-trip.</summary>
internal sealed class PublicArchiveService(
    SimfAppDbContext appDbContext,
    IOperationsToggleService operationsToggleService) : IPublicArchiveService
{
    public async Task<PublicArchive> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var visibility = await operationsToggleService
            .GetArchiveVisibilityAsync(cancellationToken);
        if (!visibility.IsVisible)
        {
            return new PublicArchive([]);
        }

        var items = await appDbContext.ArchiveEditions.AsNoTracking()
            .Where(edition => edition.IsActive)
            .OrderByDescending(edition => edition.Year)
            .Select(edition => new PublicArchiveEdition(
                edition.Id,
                edition.Year,
                edition.TitleEn,
                edition.TitleAr,
                edition.SummaryEn,
                edition.SummaryAr,
                edition.Attendees,
                edition.Sessions,
                edition.Speakers,
                edition.CoverImageRelativePath))
            .ToListAsync(cancellationToken);

        return new PublicArchive(items);
    }
}
