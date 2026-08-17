namespace SIMF.Domain.Editions;

/// <summary>
/// Which year the forum is currently running. A single row with a fixed id,
/// updated in place, on the same pattern as the registration gate.
///
/// <para>The forum recurs: an admin opens a year, content and registrations
/// accumulate against it, then the year is closed into history and the next one
/// opens. That is the permanent operating model, not a one-off.</para>
///
/// <para><b>Nothing live carried a year before this.</b> The only other year in
/// the domain is <c>ArchiveEdition.Year</c>, which is hand-typed marketing
/// content whose totals are entered by an admin rather than counted, with no
/// foreign key to any live row. So a minted badge had no expiry of any kind: the
/// gate matched a QR on its value alone and none of its checks were
/// temporal.</para>
/// </summary>
public class EventEdition
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000003");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>The year currently open, and the year a badge must carry to open
    /// a gate. Four digits, because it is encoded into the badge as two bytes and
    /// read by a scanner that has no other way to ask.</summary>
    public int Year { get; set; }

    /// <summary>When this year was opened (Saudi local).</summary>
    public DateTime OpenedAt { get; set; }

    /// <summary>When the previous year was closed into history, or null if none
    /// has been. Closing does not delete anyone: their records stay, labelled
    /// with the edition they belong to.</summary>
    public DateTime? LastClosedAt { get; set; }

    // Who opened the year is NOT kept here. It was written on every open and
    // read by nothing, while the EventEditionOpened audit row already carries the
    // actor beside the year it closed, the year it opened and the badge count —
    // and that row is the one an auditor is entitled to, because it cannot be
    // overwritten by the next open.

    /// <summary>How many attendee badges the last year-open cleared for re-issue.
    /// Kept because the number is the only evidence an operator has that the
    /// re-issue actually ran, and it is the one thing they will be asked about
    /// when a returning attendee finds their badge dead.</summary>
    public int LastReissueCount { get; set; }
}
