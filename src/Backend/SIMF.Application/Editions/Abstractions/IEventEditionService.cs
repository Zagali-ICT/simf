namespace SIMF.Application.Editions.Abstractions;

/// <summary>
/// The year the forum is currently running, and the two actions that move it.
///
/// <para>The open year is read on nearly every attendee write (it is stamped
/// onto the record) and on every gate scan (it is what expires last year's
/// badge), so it is cached rather than queried each time. The cache is
/// invalidated by the one operation that can change it.</para>
/// </summary>
public interface IEventEditionService
{
    /// <summary>The year currently open. Cached; call
    /// <see cref="OpenYearAsync"/> to move it.</summary>
    Task<int> GetOpenYearAsync(CancellationToken cancellationToken = default);

    /// <summary>The open edition, for the admin screen.</summary>
    Task<EventEditionState> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the current year into history and opens the given one.
    ///
    /// <para><b>Every attendee badge is cleared for re-issue.</b> Closing a year
    /// does not delete its attendees — their records stay, labelled with the year
    /// they belong to — but the badges they hold are not valid in the new year,
    /// so leaving them in place would mean a returning attendee arrives holding
    /// a badge every door refuses and no route to a live one. Refusing last
    /// year's badge at the gate is only correct if the holder has a way to get
    /// this year's.</para>
    /// </summary>
    Task<EventEditionOpenResult> OpenYearAsync(
        Guid actorUserId, int year, CancellationToken cancellationToken = default);
}

/// <summary>The open edition as the admin screen shows it.</summary>
public sealed record EventEditionState(
    int Year,
    DateTime OpenedAt,
    DateTime? LastClosedAt,
    int LastReissueCount);

/// <summary>What opening a year did: the year now open, and how many badges it
/// cleared for re-issue.</summary>
public sealed record EventEditionOpenResult(int Year, int BadgesCleared);
