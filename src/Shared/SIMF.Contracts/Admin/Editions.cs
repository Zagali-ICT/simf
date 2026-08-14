namespace SIMF.Contracts.Admin;

/// <summary>
/// The year the forum is currently running, as the admin screen shows it.
/// </summary>
/// <param name="Year">The open year. A badge must carry it to open a gate.</param>
/// <param name="OpenedAt">When this year was opened (Saudi local).</param>
/// <param name="LastClosedAt">When the previous year was closed into history, or
/// null if none has been.</param>
/// <param name="LastReissueCount">How many badges the last year-open cleared for
/// re-issue. It is the only evidence an operator has that the re-issue ran, and
/// the first thing they will be asked when a returning attendee finds their
/// badge dead.</param>
public sealed record AdminEventEditionResponse(
    int Year,
    DateTime OpenedAt,
    DateTime? LastClosedAt,
    int LastReissueCount);

/// <summary>
/// Body of <c>POST /admin/editions/open</c> — closes the current year into
/// history and opens the given one.
///
/// <para><b>This clears every attendee badge.</b> Closing a year does not delete
/// its attendees, but the badges they hold are not valid in the new year, so
/// they are cleared and re-issued. Refusing last year's badge at a gate is only
/// correct if the holder has a route to this year's.</para>
/// </summary>
public sealed class AdminOpenEditionRequest
{
    public int Year { get; set; }
}

/// <summary>What opening a year did.</summary>
public sealed record AdminOpenEditionResponse(int Year, int BadgesCleared);
