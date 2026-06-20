using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-479 (#11 follow-up) — which kind of meeting a "My meetings" row is.
/// Display discriminator only (not persisted), so it is safe to extend.</summary>
public enum MyMeetingKind
{
    /// <summary>A meeting the user requested with a speaker (D-269/D-475).</summary>
    SpeakerMeeting = 0,

    /// <summary>A delegation↔delegation meeting the user requested (D-478).</summary>
    DelegationMeeting = 1,
}

/// <summary>D-479 (#11 follow-up) — one row on the mobile read-only "My meetings"
/// screen: a meeting the signed-in user requested, with its current status.</summary>
public sealed record MyMeetingItem(
    MyMeetingKind Kind,
    Guid Id,
    /// <summary>The counterparty — the speaker name, or the target delegation country.</summary>
    string Title,
    /// <summary>The Arabic counterparty name (the app picks AR/EN by locale).</summary>
    string TitleArabic,
    string Subject,
    MeetingRequestStatus Status,
    DateTimeOffset? SlotStartUtc,
    DateTimeOffset CreatedAt);
