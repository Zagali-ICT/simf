using SIMF.Contracts.Programme;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>D-199 (gap doc G3, Mockup pages 16-17) — public, anonymous
/// reads over the programme <c>Session</c> surface: the agenda list and
/// the single-session detail. Read-only; the admin CRUD lives on
/// <see cref="IAdminSessionService"/>. Mirrors the
/// <c>IPublicDelegationService</c> public-read shape.</summary>
public interface IProgrammeSessionService
{
    /// <summary>Active sessions ordered by start time, optionally
    /// restricted to a single calendar day (UTC). Used by the agenda
    /// screen's Day 1/2/3 segmented control.</summary>
    Task<PublicSessions> ListAsync(
        DateOnly? day, CancellationToken cancellationToken = default);

    /// <summary>D-452 (Figma 883:2308 "تفاصيل اليوم") — the day-grouped agenda:
    /// the active programme days (date + bilingual title + has-logo flag), each
    /// with its sessions. Drives the app's day banner + day strip.</summary>
    Task<PublicProgrammeDays> ListDaysAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Full public detail for one active session, or null when
    /// the session does not exist or has been soft-deleted.</summary>
    Task<PublicSessionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>P3.2b — D-232 (D-213): the recording reference for a session
    /// that is active, <see cref="SIMF.Common.Enums.SessionStatus.Published"/>,
    /// AND has a recording — else null. Used by the stream-token endpoint (to
    /// authorise minting) and the range-streaming endpoint (to locate the
    /// file). Public visibility is gated here: a recording on an unpublished
    /// session is not reachable through this path.</summary>
    Task<SessionRecordingRef?> GetPublishedRecordingAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>P3.4 — D-235 (Completion Programme §5.4): the recorded Q&amp;A
    /// archive for a published session — the Committee-approved questions,
    /// attributed to the asker, in the moderator's display order. Empty when the
    /// session is not active+published (the archive is gated like the recording).</summary>
    Task<IReadOnlyList<PublicRecordedQuestion>> ListRecordedQuestionsAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>P4.1 — D-237 (Completion Programme §6.4.1, Mockup screen 34): the
    /// published AI session summary / محضر for one session, or null when the
    /// session is soft-deleted, has no summary, or the summary is still an
    /// unpublished draft. Gated on the summary's own <c>PublishedAt</c> (the
    /// Committee's editorial publish), independent of the broadcast
    /// <c>Session.Status</c>.</summary>
    Task<PublicSessionSummary?> GetSessionSummaryAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>D-472 (#9) — the team-approved محضر for the session's host /
    /// moderator ("ready for المحاور"), gated on <c>ApprovedAt</c> rather than the
    /// public publish. Throws 403 when <paramref name="callerUserId"/> is neither
    /// a moderator of the session (the <c>SessionModerator</c> grant) nor its host
    /// (a speaker with <c>Role = Host</c> mapped to this user); returns null when
    /// the session is soft-deleted or has no approved summary yet.</summary>
    Task<HostSessionSummary?> GetApprovedSummaryForHostAsync(
        Guid callerUserId, Guid sessionId, CancellationToken cancellationToken = default);
}

/// <summary>P3.2b — D-232: a pointer to a published session's recording on
/// disk (not a wire DTO — internal to the streaming endpoints).</summary>
public sealed record SessionRecordingRef(
    string StoredFileName, string ContentType, string FileName);
