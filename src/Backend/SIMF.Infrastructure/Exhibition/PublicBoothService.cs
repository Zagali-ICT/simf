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
                ExhibitorNameEn = b.ExhibitorNameEn,
                ExhibitorNameAr = b.ExhibitorNameAr,
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
                ExhibitorNameEn = b.ExhibitorNameEn,
                ExhibitorNameAr = b.ExhibitorNameAr,
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
