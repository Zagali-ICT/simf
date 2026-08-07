// Tests: SIMF.Api.Tests/Gates/ScanIdempotencyTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>DB-backed 24h idempotency replay store
/// (SIMF-API-GATES-001 §9).</summary>
internal sealed class ScanIdempotencyStore(
    SimfAppDbContext appDbContext,
    TimeProvider timeProvider) : IScanIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<ScanIdempotencyRecord?> TryGetAsync(
        string key, Guid gateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) { return null; }
        var cutoff = timeProvider.SimfNow() - Retention;

        var record = await appDbContext.ScanIdempotencies.AsNoTracking()
            .Where(r => r.Key == key && r.GateId == gateId && r.StoredAt >= cutoff)
            .SingleOrDefaultAsync(cancellationToken);

        if (record is null) { return null; }
        return new ScanIdempotencyRecord(
            record.Key, record.GateId, record.RequestHash,
            record.ResponseHash, record.ScanId, record.StoredAt);
    }

    public async Task PersistAsync(
        string key, Guid gateId, string requestHash, string responseHash,
        long? scanId, CancellationToken cancellationToken = default)
    {
        appDbContext.ScanIdempotencies.Add(new ScanIdempotency
        {
            Key = key,
            GateId = gateId,
            RequestHash = requestHash,
            ResponseHash = responseHash,
            ScanId = scanId,
            StoredAt = timeProvider.SimfNow(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken);
    }
}
