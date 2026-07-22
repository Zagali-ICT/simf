using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// Bi-Meeting rework — the team-defined delegation availability windows and the
/// free slots derived from them, mirroring <see cref="ISpeakerAvailabilityService"/>.
/// A window is chopped into fixed-length slots; a slot is "free" when it is in the
/// future and not already taken by a live delegation meeting. The delegation-meeting
/// request flow lets a requester from another delegation pick a free slot.
/// </summary>
public interface IDelegationAvailabilityService
{
    /// <summary>Create an availability window for a delegation (country). 404 when the
    /// country is missing/inactive, 400 when it is not an invited delegation or the
    /// range/slot length is invalid.</summary>
    Task<AdminDelegationAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, int countryId,
        CreateDelegationAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The delegation's active windows, earliest first.</summary>
    Task<IReadOnlyList<AdminDelegationAvailabilityWindow>> ListWindowsAsync(
        int countryId, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a window. 404 when missing.</summary>
    Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default);

    /// <summary>The free slots for a delegation — each window split into
    /// <c>SlotMinutes</c> chunks, dropping past slots and any already taken by a live
    /// delegation meeting involving this country.</summary>
    Task<IReadOnlyList<DelegationAvailableSlot>> GetAvailableSlotsAsync(
        int countryId, CancellationToken cancellationToken = default);
}
