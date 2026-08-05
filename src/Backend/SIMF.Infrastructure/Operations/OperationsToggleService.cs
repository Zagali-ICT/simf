// Tests: SIMF.Api.Tests/RegistrationGateTests.cs, ArchiveVisibilityTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Operations;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Operations;

/// <summary>
/// D-166 (gap doc G4) — admin + public read for the two operations
/// singletons. Both rows are seeded in EF model data; the service
/// never creates rows, only updates them.
/// </summary>
internal sealed class OperationsToggleService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<OperationsToggleService> logger) : IOperationsToggleService
{
    public async Task<RegistrationGateState> GetRegistrationGateAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await LoadRegistrationGateAsync(cancellationToken);
        return ToState(row);
    }

    public async Task<bool> IsRegistrationOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.RegistrationGate
            .AsNoTracking()
            .SingleOrDefaultAsync(g => g.Id == RegistrationGate.SingletonId, cancellationToken);
        if (row is null) { return true; } // fail-open if seeding lost the row
        if (!row.IsOpen) { return false; }
        if (row.AutoClose is { } closeAt && closeAt <= timeProvider.SimfNow())
        {
            return false;
        }
        return true;
    }

    public async Task<RegistrationGateState> UpdateRegistrationGateAsync(
        Guid actorUserId,
        UpdateRegistrationGateRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadRegistrationGateAsync(cancellationToken);
        var now = timeProvider.SimfNow();
        var changed = row.IsOpen != request.IsOpen
            || row.AutoClose != request.AutoClose;

        row.IsOpen = request.IsOpen;
        row.AutoClose = request.AutoClose;
        if (changed)
        {
            row.LastChangedAt = now;
            row.LastChangedByUserId = actorUserId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLog.WriteSuccessAsync(
                AuditEvents.RegistrationGateUpdated,
                actorUserId,
                $"isOpen={row.IsOpen}; autoClose={row.AutoClose?.ToString("O") ?? "null"}",
                cancellationToken);
            logger.LogInformation(
                "RegistrationGate updated by {ActorId}: IsOpen={IsOpen}, AutoClose={AutoClose}",
                actorUserId, row.IsOpen, row.AutoClose);
        }
        return ToState(row);
    }

    public async Task<ArchiveVisibilityState> GetArchiveVisibilityAsync(
        CancellationToken cancellationToken = default)
    {
        var row = await LoadArchiveVisibilityAsync(cancellationToken);
        return ToState(row);
    }

    public async Task<ArchiveVisibilityState> UpdateArchiveVisibilityAsync(
        Guid actorUserId,
        UpdateArchiveVisibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await LoadArchiveVisibilityAsync(cancellationToken);
        var now = timeProvider.SimfNow();
        if (row.IsVisible != request.IsVisible)
        {
            row.IsVisible = request.IsVisible;
            row.LastChangedAt = now;
            row.LastChangedByUserId = actorUserId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditLog.WriteSuccessAsync(
                AuditEvents.ArchiveVisibilityUpdated,
                actorUserId,
                $"isVisible={row.IsVisible}",
                cancellationToken);
            logger.LogInformation(
                "ArchiveVisibility updated by {ActorId}: IsVisible={IsVisible}",
                actorUserId, row.IsVisible);
        }
        return ToState(row);
    }

    private async Task<RegistrationGate> LoadRegistrationGateAsync(CancellationToken ct)
    {
        var row = await dbContext.RegistrationGate
            .SingleOrDefaultAsync(g => g.Id == RegistrationGate.SingletonId, ct);
        if (row is null)
        {
            row = new RegistrationGate
            {
                Id = RegistrationGate.SingletonId,
                IsOpen = true,
                AutoClose = null,
                LastChangedAt = timeProvider.SimfNow(),
                LastChangedByUserId = null,
            };
            dbContext.RegistrationGate.Add(row);
            await dbContext.SaveChangesAsync(ct);
        }
        return row;
    }

    private async Task<ArchiveVisibility> LoadArchiveVisibilityAsync(CancellationToken ct)
    {
        var row = await dbContext.ArchiveVisibility
            .SingleOrDefaultAsync(a => a.Id == ArchiveVisibility.SingletonId, ct);
        if (row is null)
        {
            row = new ArchiveVisibility
            {
                Id = ArchiveVisibility.SingletonId,
                IsVisible = true,
                LastChangedAt = timeProvider.SimfNow(),
                LastChangedByUserId = null,
            };
            dbContext.ArchiveVisibility.Add(row);
            await dbContext.SaveChangesAsync(ct);
        }
        return row;
    }

    private static RegistrationGateState ToState(RegistrationGate row) =>
        new(row.IsOpen, row.AutoClose, row.LastChangedAt, row.LastChangedByUserId);

    private static ArchiveVisibilityState ToState(ArchiveVisibility row) =>
        new(row.IsVisible, row.LastChangedAt, row.LastChangedByUserId);
}
