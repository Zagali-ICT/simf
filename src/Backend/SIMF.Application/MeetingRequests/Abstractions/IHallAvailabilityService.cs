using SIMF.Contracts.Programme;

namespace SIMF.Application.MeetingRequests.Abstractions;

/// <summary>
/// The team-defined hall availability windows
/// and the free slots derived from them. A window is chopped into fixed-length
/// slots; a slot is "free" when it is in the future and not already taken by a
/// meeting bound to that hall + slot. The admin
/// meeting-review flow picks a free slot to bind an accepted request to.
/// Symmetric with <see cref="ISpeakerAvailabilityService"/>.
/// </summary>
public interface IHallAvailabilityService
{
    /// <summary>Create an availability window for a hall. 404 when the hall is
    /// missing/inactive; 400 when the range or slot length is invalid.</summary>
    Task<AdminHallAvailabilityWindow> CreateWindowAsync(
        Guid actorUserId, Guid hallId,
        CreateHallAvailabilityWindowRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>The hall's active windows, earliest first.</summary>
    Task<IReadOnlyList<AdminHallAvailabilityWindow>> ListWindowsAsync(
        Guid hallId, CancellationToken cancellationToken = default);

    /// <summary>Soft-delete a window. 404 when missing.</summary>
    Task DeleteWindowAsync(
        Guid actorUserId, Guid windowId, CancellationToken cancellationToken = default);

    /// <summary>The free slots for a hall — each window split into
    /// <c>SlotMinutes</c> chunks, dropping past slots and any already taken by a
    /// bound meeting.</summary>
    Task<IReadOnlyList<HallAvailableSlot>> GetAvailableSlotsAsync(
        Guid hallId, CancellationToken cancellationToken = default);
}
