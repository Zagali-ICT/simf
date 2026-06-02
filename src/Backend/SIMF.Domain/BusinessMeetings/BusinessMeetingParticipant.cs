using SIMF.Common.Enums;
using SIMF.Domain.Companies;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — one party in a <see cref="BusinessMeeting"/>.
/// A <see cref="MeetingPartyKind.Company"/> participant carries a real FK
/// (<see cref="CompanyId"/>) to <c>Company</c> on the App DB; a
/// <see cref="MeetingPartyKind.Visitor"/> participant carries a <b>bare
/// <c>Guid</c></b> (<see cref="VisitorUserId"/>) logical FK to <c>SimfUser.Id</c>
/// on the Identity DB — no cross-DB navigation / no FK (D-157 / D-246).
///
/// <para><see cref="DisplayNameSnapshot"/> is an immutable audit snapshot of the
/// party's name at schedule time — the one allowed copy of Identity-owned data
/// (D-246), so the meeting record stays self-contained.</para>
/// </summary>
public sealed class BusinessMeetingParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="BusinessMeeting.Id"/> (cascade delete).</summary>
    public Guid BusinessMeetingId { get; set; }
    public BusinessMeeting? BusinessMeeting { get; set; }

    /// <summary>Whether this party is a company or a visitor.</summary>
    public MeetingPartyKind Kind { get; set; }

    /// <summary>Real FK to <see cref="Company.Id"/> on the App DB when
    /// <see cref="Kind"/> = Company; null otherwise.</summary>
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>Bare logical FK to <c>SimfUser.Id</c> on the Identity DB when
    /// <see cref="Kind"/> = Visitor; null otherwise. No navigation, no DB FK.</summary>
    public Guid? VisitorUserId { get; set; }

    /// <summary>Immutable audit snapshot of the party's display name at schedule
    /// time (≤ 256 chars) — the allowed Identity-data copy (D-246).</summary>
    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
