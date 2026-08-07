// Tests: SIMF.Api.Tests/PublicBoothsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Contracts.Exhibition;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibition;

/// <summary>Public, anonymous read over active booths. Mirrors
/// PublicDelegationService: AsNoTracking, IsActive filter, projection to
/// the public contract.
///
/// <para>The related-entity fields are read through the navigation
/// properties (<c>Booth.Exhibitor</c>, <c>Booth.Hall</c>), so EF emits one
/// LEFT JOIN per related row instead of a correlated subquery per field. The
/// booth-officer + company city/country fields are now inlined columns on the
/// Booth / Exhibitor rows (the shared Contact directory was removed). The legacy
/// free-text columns remain the fallback when a booth is not yet linked to a
/// curated Exhibitor, preserving the shipped wire contract.</para></summary>
internal sealed class PublicBoothService(SimfAppDbContext db) : IPublicBoothService
{
    public async Task<IReadOnlyList<PublicBoothSummary>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await db.Booths.AsNoTracking()
            // A soft-deleted exhibitor must not stay publicly visible through
            // its still-active booths: exclude booths whose linked Exhibitor is
            // inactive. An unlinked booth (b.Exhibitor == null) keeps showing on its
            // own / legacy free-text data.
            .Where(b => b.IsActive && (b.Exhibitor == null || b.Exhibitor.IsActive))
            .OrderBy(b => b.Code)
            .Select(b => new PublicBoothSummary
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                NameArabic = b.NameArabic,
                // The exhibitor name comes from the linked Exhibitor
                // when set (the curated source of truth), falling back to the
                // legacy free-text.
                ExhibitorName = b.Exhibitor != null ? b.Exhibitor.Name : b.ExhibitorName,
                ExhibitorNameArabic =
                    b.Exhibitor != null ? b.Exhibitor.NameArabic : b.ExhibitorNameArabic,
                Sector = b.Sector,
                SectorArabic = b.SectorArabic,
                HallId = b.HallId,
                MapX = b.MapX,
                MapY = b.MapY,
                // The hall display name + the booth officer fields
                // (now inlined columns on the Booth row).
                HallName = b.Hall != null ? b.Hall.Name : null,
                HallNameArabic = b.Hall != null ? b.Hall.NameArabic : null,
                OfficerName = b.OfficerName,
                OfficerPhone = b.OfficerPhone,
                OfficerEmail = b.OfficerEmail,
                // Append-only frozen wire field. The exhibitor's
                // Contact id is gone with the shared Contact directory, so it now
                // emits null.
                ExhibitorContactId = null,
                // The exhibitor company's country (now inlined as
                // Exhibitor.CountryId) for the app's corner flag on the booth logo.
                CountryId = b.Exhibitor != null ? b.Exhibitor.CountryId : null,
                // The country NAME from the Country lookup on that numeric id
                // (so the app shows the country name, not only the corner flag).
                CountryName = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.CountryId == c.Id)
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.CountryId == c.Id)
                    .Select(c => c.NameArabic).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

    public async Task<PublicBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.Booths.AsNoTracking()
            // Hide a booth whose linked Exhibitor was soft-deleted (see ListAsync).
            .Where(b => b.IsActive && b.Id == id
                && (b.Exhibitor == null || b.Exhibitor.IsActive))
            .Select(b => new PublicBoothDetail
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                NameArabic = b.NameArabic,
                // Exhibitor name from the linked Exhibitor when set,
                // else the legacy free-text.
                ExhibitorName = b.Exhibitor != null ? b.Exhibitor.Name : b.ExhibitorName,
                ExhibitorNameArabic =
                    b.Exhibitor != null ? b.Exhibitor.NameArabic : b.ExhibitorNameArabic,
                Sector = b.Sector,
                SectorArabic = b.SectorArabic,
                Description = b.Description,
                DescriptionArabic = b.DescriptionArabic,
                HallId = b.HallId,
                MapX = b.MapX,
                MapY = b.MapY,
                // Hall name + inlined booth officer fields (see ListAsync).
                HallName = b.Hall != null ? b.Hall.Name : null,
                HallNameArabic = b.Hall != null ? b.Hall.NameArabic : null,
                OfficerName = b.OfficerName,
                OfficerPhone = b.OfficerPhone,
                OfficerEmail = b.OfficerEmail,
                // Append-only frozen wire field; now emits null (the
                // exhibitor's Contact id is gone). See ListAsync.
                ExhibitorContactId = null,
                // The exhibitor company's country (now inlined as
                // Exhibitor.CountryId) for the app's corner flag on the booth logo.
                CountryId = b.Exhibitor != null ? b.Exhibitor.CountryId : null,
                // The country NAME from the Country lookup (see ListAsync).
                CountryName = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.CountryId == c.Id)
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.CountryId == c.Id)
                    .Select(c => c.NameArabic).FirstOrDefault(),
                // The exhibitor-detail extras. Website
                // is exhibitor-owned; City is now inlined on the Exhibitor; Tier
                // from the exhibitor (TierName = the enum name, the app localizes).
                Website = b.Exhibitor != null ? b.Exhibitor.Website : null,
                City = b.Exhibitor != null ? b.Exhibitor.City : null,
                CityArabic = b.Exhibitor != null ? b.Exhibitor.CityArabic : null,
                Tier = b.Exhibitor != null ? (int?)b.Exhibitor.Tier : null,
                TierName = b.Exhibitor != null && b.Exhibitor.Tier != null
                    ? b.Exhibitor.Tier.ToString()
                    : null,
                // The linked exhibitor's own id — the owner of the ExhibitorLogo the
                // app renders on the exhibitor-detail screen (null when unlinked).
                ExhibitorId = b.ExhibitorId,
            })
            .FirstOrDefaultAsync(cancellationToken);
}
