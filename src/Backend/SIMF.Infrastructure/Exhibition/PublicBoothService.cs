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
            })
            .FirstOrDefaultAsync(cancellationToken);
}
