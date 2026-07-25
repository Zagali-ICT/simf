// Tests: SIMF.Api.Tests/HallAvailabilityTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-715 (item 7, FDS-013 §15 GAP-1) — hall availability windows + the
/// free slots derived from them. Windows are team-defined; a window is chopped
/// into fixed-length slots, and a slot is offered when it is in the future (the
/// taken-by-a-bound-meeting filter lands with the accept-binds-slot flow in
/// GAP-2). Mirrors <see cref="SpeakerAvailabilityService"/>.</summary>
internal sealed class HallAvailabilityService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<HallAvailabilityService> logger) : IHallAvailabilityService
{
    private const int MinSlotMinutes = 5;
    private const int MaxSlotMinutes = 480;

    public async Task<AdminHallAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, Guid hallId,
        CreateHallAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        var hallOk = await appDbContext.Halls.AsNoTracking()
            .AnyAsync(h => h.Id == hallId && h.IsActive, cancellationToken);
        if (!hallOk)
        {
            throw new ApiException(ErrorCodes.HallNotFound, 404,
                "The hall was not found.", "لم يتم العثور على القاعة.");
        }
        if (request.SlotMinutes is < MinSlotMinutes or > MaxSlotMinutes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"Slot length must be between {MinSlotMinutes} and {MaxSlotMinutes} minutes.",
                $"يجب أن تتراوح مدة الفترة بين {MinSlotMinutes} و {MaxSlotMinutes} دقيقة.");
        }
        if (request.End <= request.Start
            || (request.End - request.Start).TotalMinutes < request.SlotMinutes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "The window must end after it starts and fit at least one slot.",
                "يجب أن تنتهي الفترة بعد بدايتها وأن تتّسع لفترة واحدة على الأقل.");
        }

        var now = timeProvider.GetUtcNow();
        var window = new HallAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            HallId = hallId,
            Start = request.Start,
            End = request.End,
            SlotMinutes = request.SlotMinutes,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
        };
        appDbContext.HallAvailabilityWindows.Add(window);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallAvailabilityWindowCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={window.Id}; hallId={hallId}",
        }, cancellationToken);

        logger.LogInformation(
            "Hall availability window {WindowId} created for hall {HallId} by {Actor}",
            window.Id, hallId, actorUserId);

        return ToDto(window);
    }

    public async Task<IReadOnlyList<AdminHallAvailabilityWindow>> ListWindowsAsync(
        Guid hallId, CancellationToken cancellationToken = default) =>
        await appDbContext.HallAvailabilityWindows.AsNoTracking()
            .Where(w => w.HallId == hallId && w.IsActive)
            .OrderBy(w => w.Start)
            .Select(w => new AdminHallAvailabilityWindow(
                w.Id, w.HallId, w.Start, w.End, w.SlotMinutes, w.IsActive, w.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default)
    {
        var window = await appDbContext.HallAvailabilityWindows
            .SingleOrDefaultAsync(w => w.Id == windowId && w.IsActive, cancellationToken)
            ?? throw new ApiException(ErrorCodes.HallAvailabilityWindowNotFound, 404,
                "The availability window was not found.", "لم يتم العثور على فترة التوفّر.");
        window.IsActive = false;
        window.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.HallAvailabilityWindowDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={windowId}",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<HallAvailableSlot>> GetAvailableSlotsAsync(
        Guid hallId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var windows = await appDbContext.HallAvailabilityWindows.AsNoTracking()
            .Where(w => w.HallId == hallId && w.IsActive && w.End > now)
            .OrderBy(w => w.Start)
            .ToListAsync(cancellationToken);
        if (windows.Count == 0)
        {
            return Array.Empty<HallAvailableSlot>();
        }

        // D-716 (GAP-2) — the slots already taken by a bound meeting are removed.
        // "Taken" = a request in the slot-holding live set
        // (`MeetingRequestStatuses.SlotHolding` = Accepted + AwaitingSpeaker + Done), the
        // single authority the accept re-check + the DB indexes also key off. Load
        // the busy ranges once, then drop any generated slot that overlaps one
        // (half-open overlap, the same rule the accept re-check uses).
        var busy = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.HallId == hallId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStart != null && r.SlotEnd != null)
            .Select(r => new { Start = r.SlotStart!.Value, End = r.SlotEnd!.Value })
            .ToListAsync(cancellationToken);

        // Bi-Meeting rework — a DELEGATION meeting bound to this hall occupies the slot
        // too. Once delegation meetings bind halls (P3), the hall free-slot read MUST
        // subtract them as well, or a hall slot held by a delegation meeting is offered
        // as free and double-booked across a speaker + a delegation meeting.
        var delegationBusy = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => r.HallId == hallId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStart != null && r.SlotEnd != null)
            .Select(r => new { Start = r.SlotStart!.Value, End = r.SlotEnd!.Value })
            .ToListAsync(cancellationToken);
        busy.AddRange(delegationBusy);

        var slots = new List<HallAvailableSlot>();
        foreach (var w in windows)
        {
            var length = TimeSpan.FromMinutes(w.SlotMinutes);
            var slotStart = w.Start;
            while (slotStart + length <= w.End)
            {
                var slotEnd = slotStart + length;
                if (slotStart >= now
                    && !busy.Any(b => b.Start < slotEnd && slotStart < b.End))
                {
                    slots.Add(new HallAvailableSlot(slotStart, slotEnd));
                }
                slotStart = slotEnd;
            }
        }
        return slots;
    }

    private static AdminHallAvailabilityWindow ToDto(HallAvailabilityWindow w) =>
        new(w.Id, w.HallId, w.Start, w.End, w.SlotMinutes, w.IsActive, w.CreatedAt);
}
