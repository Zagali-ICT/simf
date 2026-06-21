using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// D-474 (#11, Group G phase 1) — the team-defined speaker availability windows
/// and the free slots derived from them. A window is chopped into fixed-length
/// slots; a slot is "free" when it is in the future and not already taken by an
/// accepted speaker meeting. The VIP-meeting request flow (phase 1b) lets a VIP
/// pick a free slot.
/// </summary>
public interface ISpeakerAvailabilityService
{
    /// <summary>Create an availability window for a speaker. 404 when the speaker
    /// is missing/inactive; 400 when the range or slot length is invalid.</summary>
    Task<AdminSpeakerAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, Guid speakerId,
        CreateSpeakerAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The speaker's active windows, earliest first.</summary>
    Task<IReadOnlyList<AdminSpeakerAvailabilityWindow>> ListWindowsAsync(
        Guid speakerId, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a window. 404 when missing.</summary>
    Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default);

    /// <summary>The free slots for a speaker — each window split into
    /// <c>SlotMinutes</c> chunks, dropping past slots and any already taken by an
    /// accepted meeting request.</summary>
    Task<IReadOnlyList<SpeakerAvailableSlot>> GetAvailableSlotsAsync(
        Guid speakerId, CancellationToken cancellationToken = default);
}
