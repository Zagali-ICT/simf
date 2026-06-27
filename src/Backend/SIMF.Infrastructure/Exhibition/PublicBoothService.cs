// Tests: SIMF.Api.Tests/PublicBoothsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Contracts.Exhibition;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibition;

/// <summary>D-199 — public, anonymous read over active booths. Mirrors
/// PublicDelegationService: AsNoTracking, IsActive filter, projection to
/// the public contract.</summary>
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
                ExhibitorName = b.ExhibitorId == null
                    ? b.ExhibitorName
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.Name).FirstOrDefault(),
                ExhibitorNameArabic = b.ExhibitorId == null
                    ? b.ExhibitorNameArabic
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.NameArabic).FirstOrDefault(),
                Sector = b.Sector,
                SectorArabic = b.SectorArabic,
                HallId = b.HallId,
                MapX = b.MapX,
                MapY = b.MapY,
                // D-432 — the hall display name (entity already has it) + the
                // booth officer resolved Contact-first (the de-duplicated D-260
                // directory record), falling back to the legacy inline columns.
                HallName = b.HallId == null
                    ? null
                    : db.Halls.Where(h => h.Id == b.HallId)
                        .Select(h => h.Name).FirstOrDefault(),
                HallNameArabic = b.HallId == null
                    ? null
                    : db.Halls.Where(h => h.Id == b.HallId)
                        .Select(h => h.NameArabic).FirstOrDefault(),
                OfficerName = b.ContactId == null
                    ? b.OfficerName
                    : (db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.NameArabic != "" ? c.NameArabic : c.Name)
                        .FirstOrDefault() ?? b.OfficerName),
                OfficerPhone = b.ContactId == null
                    ? b.OfficerPhone
                    : db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.PhonePrimary).FirstOrDefault(),
                OfficerEmail = b.ContactId == null
                    ? b.OfficerEmail
                    : db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.Email).FirstOrDefault(),
                // P6 — D-440: the exhibitor's Contact id (the CompanyLogo owner),
                // so the app can render the real booth logo (null when unlinked).
                ExhibitorContactId = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.ContactId).FirstOrDefault(),
                // D-456: the exhibitor company's country (Exhibitor → Contact →
                // CountryId) for the app's corner flag on the booth logo.
                CountryId = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Contact != null ? e.Contact.CountryId : null)
                        .FirstOrDefault(),
                // #9: the country NAME from the Country lookup on that numeric id
                // (so the app shows the country name, not only the corner flag).
                CountryName = db.Countries
                    .Where(c => db.Exhibitors.Any(e => e.Id == b.ExhibitorId
                        && e.Contact != null && e.Contact.CountryId == c.Id))
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => db.Exhibitors.Any(e => e.Id == b.ExhibitorId
                        && e.Contact != null && e.Contact.CountryId == c.Id))
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
                ExhibitorName = b.ExhibitorId == null
                    ? b.ExhibitorName
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.Name).FirstOrDefault(),
                ExhibitorNameArabic = b.ExhibitorId == null
                    ? b.ExhibitorNameArabic
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.NameArabic).FirstOrDefault(),
                Sector = b.Sector,
                SectorArabic = b.SectorArabic,
                Description = b.Description,
                DescriptionArabic = b.DescriptionArabic,
                HallId = b.HallId,
                MapX = b.MapX,
                MapY = b.MapY,
                // D-432 — hall name + Contact-first officer (see ListAsync).
                HallName = b.HallId == null
                    ? null
                    : db.Halls.Where(h => h.Id == b.HallId)
                        .Select(h => h.Name).FirstOrDefault(),
                HallNameArabic = b.HallId == null
                    ? null
                    : db.Halls.Where(h => h.Id == b.HallId)
                        .Select(h => h.NameArabic).FirstOrDefault(),
                OfficerName = b.ContactId == null
                    ? b.OfficerName
                    : (db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.NameArabic != "" ? c.NameArabic : c.Name)
                        .FirstOrDefault() ?? b.OfficerName),
                OfficerPhone = b.ContactId == null
                    ? b.OfficerPhone
                    : db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.PhonePrimary).FirstOrDefault(),
                OfficerEmail = b.ContactId == null
                    ? b.OfficerEmail
                    : db.Contacts.Where(c => c.Id == b.ContactId)
                        .Select(c => c.Email).FirstOrDefault(),
                // P6 — D-440: exhibitor's Contact id (CompanyLogo owner); see ListAsync.
                ExhibitorContactId = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(c => c.Id == b.ExhibitorId)
                        .Select(c => c.ContactId).FirstOrDefault(),
                // D-456: the exhibitor company's country (Exhibitor → Contact →
                // CountryId) for the app's corner flag on the booth logo.
                CountryId = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Contact != null ? e.Contact.CountryId : null)
                        .FirstOrDefault(),
                // #9: the country NAME from the Country lookup (see ListAsync).
                CountryName = db.Countries
                    .Where(c => db.Exhibitors.Any(e => e.Id == b.ExhibitorId
                        && e.Contact != null && e.Contact.CountryId == c.Id))
                    .Select(c => c.Name).FirstOrDefault(),
                CountryNameArabic = db.Countries
                    .Where(c => db.Exhibitors.Any(e => e.Id == b.ExhibitorId
                        && e.Contact != null && e.Contact.CountryId == c.Id))
                    .Select(c => c.NameArabic).FirstOrDefault(),
                // Wave 3 (Figma 1439:11881): the exhibitor-detail extras. Website
                // is exhibitor-owned; City comes from the exhibitor's Contact; Tier
                // from the exhibitor (TierName = the enum name, the app localizes).
                Website = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Website).FirstOrDefault(),
                City = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Contact != null ? e.Contact.City : null)
                        .FirstOrDefault(),
                CityArabic = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Contact != null ? e.Contact.CityArabic : null)
                        .FirstOrDefault(),
                Tier = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => (int?)e.Tier).FirstOrDefault(),
                TierName = b.ExhibitorId == null
                    ? null
                    : db.Exhibitors.Where(e => e.Id == b.ExhibitorId)
                        .Select(e => e.Tier != null ? e.Tier.ToString() : null)
                        .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);
}
