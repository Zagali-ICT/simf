// Tests: SIMF.Api.Tests/SpeakerAvailabilityTests.cs
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

/// <summary>D-474 (#11, Group G phase 1) — speaker availability windows + the free
/// slots derived from them. Windows are team-defined; a window is chopped into
/// fixed-length slots, and a slot is offered when it is in the future and not
/// taken by an accepted meeting (half-open overlap, mirroring the BusinessMeeting
/// overlap pattern).</summary>
internal sealed class SpeakerAvailabilityService(
    SimfAppDbContext appDbContext,
    IForumWindowService forumWindow,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<SpeakerAvailabilityService> logger) : ISpeakerAvailabilityService
{
    private const int MinSlotMinutes = 5;
    private const int MaxSlotMinutes = 480;

    /// <summary>The event's local-day boundary (KSA, UTC+3) — the same convention
    /// the programme uses to bucket a session to a Riyadh calendar day. A window's
    /// start/end are converted to this zone before the forum-day bound is checked.</summary>
    private static readonly TimeSpan EventOffset = TimeSpan.FromHours(3);

    public async Task<AdminSpeakerAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, Guid speakerId,
        CreateSpeakerAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default)
    {
        var speakerOk = await appDbContext.Speakers.AsNoTracking()
            .AnyAsync(s => s.Id == speakerId && s.IsActive, cancellationToken);
        if (!speakerOk)
        {
            throw new ApiException(ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.", "لم يتم العثور على المتحدّث.");
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

        // D-753 — forum-day bound: an availability window may only be defined on the
        // authored event days (MIN/MAX over active ProgrammeDay.Date — NOT the stale
        // OrganizationProfile placeholder). The window's start and end are converted
        // to the event-local (+03:00) calendar date and both must fall inside
        // [MinDate, MaxDate]. When no programme days are seeded the window is null and
        // no bound is applied.
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
        var window = new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            SlotMinutes = request.SlotMinutes,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = actorUserId,
        };
        appDbContext.SpeakerAvailabilityWindows.Add(window);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerAvailabilityWindowCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={window.Id}; speakerId={speakerId}",
        }, cancellationToken);

        logger.LogInformation(
            "Speaker availability window {WindowId} created for speaker {SpeakerId} by {Actor}",
            window.Id, speakerId, actorUserId);

        return ToDto(window);
    }

    public async Task<IReadOnlyList<AdminSpeakerAvailabilityWindow>> ListWindowsAsync(
        Guid speakerId, CancellationToken cancellationToken = default) =>
        await appDbContext.SpeakerAvailabilityWindows.AsNoTracking()
            .Where(w => w.SpeakerId == speakerId && w.IsActive)
            .OrderBy(w => w.StartUtc)
            .Select(w => new AdminSpeakerAvailabilityWindow(
                w.Id, w.SpeakerId, w.StartUtc, w.EndUtc, w.SlotMinutes, w.IsActive, w.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default)
    {
        var window = await appDbContext.SpeakerAvailabilityWindows
            .SingleOrDefaultAsync(w => w.Id == windowId && w.IsActive, cancellationToken)
            ?? throw new ApiException(ErrorCodes.SpeakerAvailabilityWindowNotFound, 404,
                "The availability window was not found.", "لم يتم العثور على فترة التوفّر.");
        window.IsActive = false;
        window.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerAvailabilityWindowDeleted,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"windowId={windowId}",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SpeakerAvailableSlot>> GetAvailableSlotsAsync(
        Guid speakerId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var windows = await appDbContext.SpeakerAvailabilityWindows.AsNoTracking()
            .Where(w => w.SpeakerId == speakerId && w.IsActive && w.EndUtc > now)
            .OrderBy(w => w.StartUtc)
            .ToListAsync(cancellationToken);
        if (windows.Count == 0)
        {
            return Array.Empty<SpeakerAvailableSlot>();
        }

        // Slots already taken by a LIVE meeting for this speaker. "Taken" = a
        // request in the slot-holding set (`MeetingRequestStatuses.SlotHolding` =
        // Accepted + AwaitingSpeaker + Done) — the single authority the accept re-check +
        // the DB filtered-unique indexes also key off. Keying on Accepted only would
        // advertise a slot already held by a hall-bound AwaitingSpeaker meeting as
        // free, creating a request the admin could never accept (mirrors
        // HallAvailabilityService's taken-filter).
        var taken = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .Where(r => r.SpeakerId == speakerId
                && MeetingRequestStatuses.SlotHolding.Contains(r.Status)
                && r.SlotStartUtc != null && r.SlotEndUtc != null)
            .Select(r => new { Start = r.SlotStartUtc!.Value, End = r.SlotEndUtc!.Value })
            .ToListAsync(cancellationToken);

        var slots = new List<SpeakerAvailableSlot>();
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
                    slots.Add(new SpeakerAvailableSlot(slotStart, slotEnd));
                }
                slotStart = slotEnd;
            }
        }
        return slots;
    }

    private static AdminSpeakerAvailabilityWindow ToDto(SpeakerAvailabilityWindow w) =>
        new(w.Id, w.SpeakerId, w.StartUtc, w.EndUtc, w.SlotMinutes, w.IsActive, w.CreatedAt);
}
