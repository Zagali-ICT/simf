using SIMF.Contracts.Account;

namespace SIMF.Application.MyArea;

/// <summary>
/// The My-Area (منطقتي) personal dashboard read model — App Screen 14
/// (Page_014). Read-only aggregates over the App DB (held seat bookings,
/// accepted speaker meetings, confirmed business meetings) plus the user's
/// profile + the account avatar. The avatar is resolved on read from the
/// Identity side (D-157 — no cross-DB join). D-249.
/// </summary>
public interface IMyAreaService
{
    /// <summary>The dashboard: identity card + the two counters + today's
    /// merged, time-ordered schedule.</summary>
    Task<MyAreaDashboard> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Every calendar event (held sessions + accepted speaker meetings
    /// + confirmed business meetings), across all days, time-ordered — for the
    /// <c>calendar.ics</c> export.</summary>
    Task<IReadOnlyList<MyAreaCalendarEvent>> GetCalendarEventsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>The contact-card (vCard) data for the <c>contact-card.vcf</c>
    /// export.</summary>
    Task<MyAreaContactCard> GetContactCardAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>The "my sessions" list (App "تفاصيل الجلسات", Figma 1388:9067):
    /// the user's booked / joined sessions across all days, each with the
    /// per-user heart + attended flag, time-ordered. The app partitions these
    /// into the القادمة / حضرتها / فاتتني / الأرشيف tabs client-side.</summary>
    Task<MyAreaSessions> GetMySessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
