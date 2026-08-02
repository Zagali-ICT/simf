namespace SIMF.Contracts.Admin;

/// <summary>
/// Create an admin notification broadcast (Control Panel "Announcements" desk).
/// The enum-valued fields travel as name strings and are parsed server-side —
/// this survives the CP JS-interop + BFF-proxy hops cleanly and matches the
/// existing <c>AdminNotifyVipsRequest</c> convention. The server inserts one
/// Pending job and a background worker fans it out (in-app row + email per
/// recipient).
/// </summary>
public sealed class AdminCreateBroadcastRequest
{
    /// <summary>"Session" or "Audience".</summary>
    public string TargetMode { get; set; } = string.Empty;

    /// <summary>Required when <see cref="TargetMode"/> is "Session" — the target
    /// session. Recipients = everyone with an active seat reservation in it.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Required when <see cref="TargetMode"/> is "Audience" — one of
    /// "ApprovedAppUsers", "EventAttendees", "EveryoneIncludingPending".</summary>
    public string? AudienceScope { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Notification severity — "Info" (default), "Success", "Warning" or
    /// "Error". Drives the tile colour/icon in the app.</summary>
    public string Severity { get; set; } = "Info";
}

/// <summary>Estimate how many recipients a target would reach — powers the composer's
/// live recipient count before the admin sends.</summary>
public sealed class AdminBroadcastEstimateRequest
{
    public string TargetMode { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public string? AudienceScope { get; set; }
}

/// <summary>The estimated distinct-recipient count for a target.</summary>
public sealed record AdminBroadcastEstimateResult(int EstimatedRecipients);

/// <summary>Result of accepting a broadcast — the queued job id + the estimated
/// recipient count at submit time (the worker computes and stamps the final count).</summary>
public sealed record AdminBroadcastResult(Guid Id, int EstimatedRecipients);

/// <summary>One row in the Announcements history grid — a past broadcast with its
/// delivery status + counters. The composer's display-name + session title are
/// projected so the grid needs no second fetch.</summary>
public sealed record AdminBroadcastSummary(
    Guid Id,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    string TargetMode,
    Guid? SessionId,
    string? SessionTitle,
    string? AudienceScope,
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string Severity,
    string Status,
    int TotalRecipients,
    int Dispatched,
    int EmailsEnqueued,
    int Skipped,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error);
