// Tests: SIMF.Api.Tests/PublicBoothsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Contracts.Exhibition;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibition;

/// <summary>D-199 — public, anonymous read over active booths. Mirrors
/// PublicDelegationService: AsNoTracking, IsActive filter, projection to
/// the public contract.
///
/// <para>A5 — the related-entity fields are read through the navigation
/// properties (<c>Booth.Exhibitor</c>, <c>Booth.Hall</c>,
/// <c>Booth.OfficerContact</c>, and the existing <c>Exhibitor.Contact</c>), so
/// EF emits one LEFT JOIN per related row instead of a correlated subquery per
/// field. The legacy free-text columns remain the fallback when a booth is not
/// yet linked to a curated Exhibitor / Contact (D-219 wire contract).</para></summary>
internal sealed class PublicBoothService(SimfAppDbContext db) : IPublicBoothService
{
    public async Task<IReadOnlyList<PublicBoothSummary>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await db.Booths.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Code)
            .Select(b => new PublicBoothSummary
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                NameArabic = b.NameArabic,
                // B1 — D-222: the exhibitor name comes from the linked Exhibitor
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
                // D-432 — the hall display name + the booth officer resolved
                // Contact-first (the de-duplicated D-260 directory record),
                // falling back to the legacy inline columns.
                HallName = b.Hall != null ? b.Hall.Name : null,
                HallNameArabic = b.Hall != null ? b.Hall.NameArabic : null,
                OfficerName = b.OfficerContact != null
                    ? ((b.OfficerContact.NameArabic != ""
                        ? b.OfficerContact.NameArabic
                        : b.OfficerContact.Name) ?? b.OfficerName)
                    : b.OfficerName,
                OfficerPhone = b.OfficerContact != null
                    ? b.OfficerContact.PhonePrimary
                    : b.OfficerPhone,
                OfficerEmail = b.OfficerContact != null
                    ? b.OfficerContact.Email
                    : b.OfficerEmail,
                // P6 — D-440: the exhibitor's Contact id (the CompanyLogo owner),
                // so the app can render the real booth logo (null when unlinked).
                ExhibitorContactId = b.Exhibitor != null ? b.Exhibitor.ContactId : null,
                // D-456: the exhibitor company's country (Exhibitor → Contact →
                // CountryId) for the app's corner flag on the booth logo.
                CountryId = b.Exhibitor != null && b.Exhibitor.Contact != null
                    ? b.Exhibitor.Contact.CountryId
                    : null,
                // #9: the country NAME from the Country lookup on that numeric id
                // (so the app shows the country name, not only the corner flag).
                CountryName = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.Contact != null
                        && b.Exhibitor.Contact.CountryId == c.Id)
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.Contact != null
                        && b.Exhibitor.Contact.CountryId == c.Id)
                    .Select(c => c.NameArabic).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

    public async Task<PublicBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await db.Booths.AsNoTracking()
            .Where(b => b.IsActive && b.Id == id)
            .Select(b => new PublicBoothDetail
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                NameArabic = b.NameArabic,
                // B1 — D-222: exhibitor name from the linked Exhibitor when set,
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
                // D-432 — hall name + Contact-first officer (see ListAsync).
                HallName = b.Hall != null ? b.Hall.Name : null,
                HallNameArabic = b.Hall != null ? b.Hall.NameArabic : null,
                OfficerName = b.OfficerContact != null
                    ? ((b.OfficerContact.NameArabic != ""
                        ? b.OfficerContact.NameArabic
                        : b.OfficerContact.Name) ?? b.OfficerName)
                    : b.OfficerName,
                OfficerPhone = b.OfficerContact != null
                    ? b.OfficerContact.PhonePrimary
                    : b.OfficerPhone,
                OfficerEmail = b.OfficerContact != null
                    ? b.OfficerContact.Email
                    : b.OfficerEmail,
                // P6 — D-440: exhibitor's Contact id (CompanyLogo owner); see ListAsync.
                ExhibitorContactId = b.Exhibitor != null ? b.Exhibitor.ContactId : null,
                // D-456: the exhibitor company's country (Exhibitor → Contact →
                // CountryId) for the app's corner flag on the booth logo.
                CountryId = b.Exhibitor != null && b.Exhibitor.Contact != null
                    ? b.Exhibitor.Contact.CountryId
                    : null,
                // #9: the country NAME from the Country lookup (see ListAsync).
                CountryName = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.Contact != null
                        && b.Exhibitor.Contact.CountryId == c.Id)
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => b.Exhibitor != null && b.Exhibitor.Contact != null
                        && b.Exhibitor.Contact.CountryId == c.Id)
                    .Select(c => c.NameArabic).FirstOrDefault(),
                // Wave 3 (Figma 1439:11881): the exhibitor-detail extras. Website
                // is exhibitor-owned; City comes from the exhibitor's Contact; Tier
                // from the exhibitor (TierName = the enum name, the app localizes).
                Website = b.Exhibitor != null ? b.Exhibitor.Website : null,
                City = b.Exhibitor != null && b.Exhibitor.Contact != null
                    ? b.Exhibitor.Contact.City
                    : null,
                CityArabic = b.Exhibitor != null && b.Exhibitor.Contact != null
                    ? b.Exhibitor.Contact.CityArabic
                    : null,
                Tier = b.Exhibitor != null ? (int?)b.Exhibitor.Tier : null,
                TierName = b.Exhibitor != null && b.Exhibitor.Tier != null
                    ? b.Exhibitor.Tier.ToString()
                    : null,
            })
            .FirstOrDefaultAsync(cancellationToken);
}
