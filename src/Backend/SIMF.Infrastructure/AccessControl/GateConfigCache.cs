// Tests: SIMF.Api.Tests/Gates/GateConfigCacheTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>D-148 — IMemoryCache-backed read-through cache. 5-minute TTL
/// per D-148 reasoning. <c>AdminGateService</c> calls <see cref="Invalidate"/>
/// on every Update / Deactivate; the cache also expires entries on its own
/// schedule so a stale read can never persist beyond the TTL.</summary>
internal sealed class GateConfigCache(
    IMemoryCache memoryCache,
    SimfAppDbContext appDbContext) : IGateConfigCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "gate-config:";

    public async Task<GateConfigSnapshot?> GetAsync(
        Guid gateId, CancellationToken cancellationToken = default)
    {
        var cacheKey = KeyPrefix + gateId.ToString("N");
        if (memoryCache.TryGetValue<GateConfigSnapshot>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var gate = await appDbContext.Gates.AsNoTracking()
            .Where(g => g.Id == gateId)
            .Select(g => new
            {
                g.Id, g.Code, g.Name, g.NameArabic, g.DirectionMode, g.IsActive,
                g.HallId,
                AllowRaw = g.AllowedProfileTypes
                    .Select(a => a.ProfileTypeId).ToList(),
                Operators = g.Assignments
                    .Where(a => a.IsActive)
                    .Select(a => a.UserId).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (gate is null) { return null; }

        IReadOnlyList<Guid> filtered;
        if (gate.AllowRaw.Count == 0)
        {
            filtered = Array.Empty<Guid>();
        }
        else
        {
            var activeIds = await appDbContext.ProfileTypes.AsNoTracking()
                .Where(p => p.IsActive && gate.AllowRaw.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            filtered = activeIds;
        }

        var snapshot = new GateConfigSnapshot(
            gate.Id, gate.Code, gate.Name, gate.NameArabic,
            gate.DirectionMode, gate.IsActive,
            gate.AllowRaw, filtered, gate.Operators, gate.HallId);

        memoryCache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
        });

        return snapshot;
    }

    public void Invalidate(Guid gateId) =>
        memoryCache.Remove(KeyPrefix + gateId.ToString("N"));
}
