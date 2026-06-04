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
            })
            .FirstOrDefaultAsync(cancellationToken);
}
