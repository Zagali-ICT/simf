// Tests: SIMF.Api.Tests/DelegationAvailabilityTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>Bi-Meeting rework — delegation availability windows + the free slots
/// derived from them, mirroring <see cref="SpeakerAvailabilityService"/>. Windows are
/// team-defined per delegation (country); a window is chopped into fixed-length slots,
/// and a slot is offered when it is in the future and not taken by a live delegation
/// meeting involving that country (half-open overlap).</summary>
internal sealed class DelegationAvailabilityService(
    SimfAppDbContext appDbContext,
    IForumWindowService forumWindow,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<DelegationAvailabilityService> logger) : IDelegationAvailabilityService
{
    private const int MinSlotMinutes = 5;
    private const int MaxSlotMinutes = 480;

    /// <summary>The event's local-day boundary (KSA, UTC+3) — same convention as the
    /// speaker/hall availability services for the forum-day bound check.</summary>
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    public async Task<AdminDelegationAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, int countryId,
        CreateDelegationAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        // The delegation must be an active, invited country (only invited delegations
        // can hold bilateral meetings — mirrors DelegationMeetingRequestService).
        var invited = await appDbContext.Countries.AsNoTracking()
            .AnyAsync(c => c.Id == countryId && c.IsActive && c.IsInvited, cancellationToken);
        if (!invited)
        {
            throw new ApiException(ErrorCodes.DelegateCountryNotInvited, 400,
                "The delegation is not an invited country.",
                "الوفد ليس من الدول المدعوّة.");
        }
        if (request.SlotMinutes is < MinSlotMinutes or > MaxSlotMinutes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"Slot length must be between {MinSlotMinutes} and {MaxSlotMinutes} minutes.",
                $"يجب أن تتراوح مدة الفترة بين {MinSlotMinutes} و {MaxSlotMinutes} دقيقة.");
        }
        if (request.EndUtc <= request.StartUtc
            || (request.EndUtc - request.StartUtc).TotalMinutes < request.SlotMinutes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "The window must end after it starts and fit at least one slot.",
                "يجب أن تنتهي الفترة بعد بدايتها وأن تتّسع لفترة واحدة على الأقل.");
        }

        // Forum-day bound — a window may only be defined on the authored event days
        // (identical rule to SpeakerAvailabilityService).
        var forum = await forumWindow.GetForumDaysAsync(cancellationToken);
        if (forum is { } bounds)
        {
            var startDate = DateOnly.FromDateTime(request.StartUtc.ToOffset(EventOffset).DateTime);
            var endDate = DateOnly.FromDateTime(request.EndUtc.ToOffset(EventOffset).DateTime);
            if (startDate < bounds.MinDate || endDate > bounds.MaxDate)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, 400,
                    $"Availability windows can only be set within the forum days "
                        + $"({bounds.MinDate:dd-MM-yyyy} to {bounds.MaxDate:dd-MM-yyyy}).",
                    $"لا يمكن تحديد فترات التوفّر إلا خلال أيام الملتقى "
                        + $"({bounds.MinDate:dd-MM-yyyy} إلى {bounds.MaxDate:dd-MM-yyyy}).");
            }
        }

        var now = timeProvider.GetUtcNow();
        var window = new DelegationAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            CountryId = countryId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            SlotMinutes = request.SlotMinutes,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
        };
        appDbContext.DelegationAvailabilityWindows.Add(window);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationAvailabilityWindowCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={window.Id}; countryId={countryId}",
        }, cancellationToken);

        logger.LogInformation(
            "Delegation availability window {WindowId} created for country {CountryId} by {Actor}",
            window.Id, countryId, actorUserId);

        return ToDto(window);
    }

    public async Task<IReadOnlyList<AdminDelegationAvailabilityWindow>> ListWindowsAsync(
        int countryId, CancellationToken cancellationToken = default) =>
        await appDbContext.DelegationAvailabilityWindows.AsNoTracking()
            .Where(w => w.CountryId == countryId && w.IsActive)
            .OrderBy(w => w.StartUtc)
            .Select(w => new AdminDelegationAvailabilityWindow(
                w.Id, w.CountryId, w.StartUtc, w.EndUtc, w.SlotMinutes, w.IsActive, w.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default)
    {
        var window = await appDbContext.DelegationAvailabilityWindows
            .SingleOrDefaultAsync(w => w.Id == windowId && w.IsActive, cancellationToken)
            ?? throw new ApiException(ErrorCodes.DelegationAvailabilityWindowNotFound, 404,
                "The availability window was not found.", "لم يتم العثور على فترة التوفّر.");
        window.IsActive = false;
        window.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationAvailabilityWindowDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={windowId}",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DelegationAvailableSlot>> GetAvailableSlotsAsync(
        int countryId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var windows = await appDbContext.DelegationAvailabilityWindows.AsNoTracking()
            .Where(w => w.CountryId == countryId && w.IsActive && w.EndUtc > now)
            .OrderBy(w => w.StartUtc)
            .ToListAsync(cancellationToken);
        if (windows.Count == 0)
        {
            return Array.Empty<DelegationAvailableSlot>();
        }

        // Slots already taken by a LIVE delegation meeting involving this country
        // (as requester OR target). "Taken" = a request in the slot-holding set
        // (`MeetingRequestStatuses.SlotHolding` = Accepted + AwaitingSpeaker + Done) —
        // the single authority the accept re-check + the DB indexes also key off.
        var taken = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .Where(r => (r.RequestingCountryId == countryId || r.TargetCountryId == countryId)
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStartUtc != null && r.SlotEndUtc != null)
            .Select(r => new { Start = r.SlotStartUtc!.Value, End = r.SlotEndUtc!.Value })
            .ToListAsync(cancellationToken);

        var slots = new List<DelegationAvailableSlot>();
        foreach (var w in windows)
        {
            var length = TimeSpan.FromMinutes(w.SlotMinutes);
            var slotStart = w.StartUtc;
            while (slotStart + length <= w.EndUtc)
            {
                var slotEnd = slotStart + length;
                var isPast = slotStart < now;
                var isTaken = taken.Any(t => t.Start < slotEnd && slotStart < t.End);
                if (!isPast && !isTaken)
                {
                    slots.Add(new DelegationAvailableSlot(slotStart, slotEnd));
                }
                slotStart = slotEnd;
            }
        }
        return slots;
    }

    private static AdminDelegationAvailabilityWindow ToDto(DelegationAvailabilityWindow w) =>
        new(w.Id, w.CountryId, w.StartUtc, w.EndUtc, w.SlotMinutes, w.IsActive, w.CreatedAt);
}
