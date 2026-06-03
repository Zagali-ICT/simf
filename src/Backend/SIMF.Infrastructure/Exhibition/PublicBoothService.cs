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
                NameEn = b.NameEn,
                NameAr = b.NameAr,
                // B1 — D-222: the exhibitor name comes from the linked Company
                // when set (the curated source of truth), falling back to the
                // legacy free-text. The wire field names are unchanged (D-219).
                ExhibitorNameEn = b.CompanyId == null
                    ? b.ExhibitorNameEn
                    : db.Companies.Where(c => c.Id == b.CompanyId)
                        .Select(c => c.Name).FirstOrDefault(),
                ExhibitorNameAr = b.CompanyId == null
                    ? b.ExhibitorNameAr
                    : db.Companies.Where(c => c.Id == b.CompanyId)
                        .Select(c => c.NameArabic).FirstOrDefault(),
                SectorEn = b.SectorEn,
                SectorAr = b.SectorAr,
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
                NameEn = b.NameEn,
                NameAr = b.NameAr,
                // B1 — D-222: exhibitor name from the linked Company when set,
                // else the legacy free-text (wire field names unchanged).
                ExhibitorNameEn = b.CompanyId == null
                    ? b.ExhibitorNameEn
                    : db.Companies.Where(c => c.Id == b.CompanyId)
                        .Select(c => c.Name).FirstOrDefault(),
                ExhibitorNameAr = b.CompanyId == null
                    ? b.ExhibitorNameAr
                    : db.Companies.Where(c => c.Id == b.CompanyId)
                        .Select(c => c.NameArabic).FirstOrDefault(),
                SectorEn = b.SectorEn,
                SectorAr = b.SectorAr,
                DescriptionEn = b.DescriptionEn,
                DescriptionAr = b.DescriptionAr,
                HallId = b.HallId,
                MapX = b.MapX,
                MapY = b.MapY,
            })
            .FirstOrDefaultAsync(cancellationToken);
}
