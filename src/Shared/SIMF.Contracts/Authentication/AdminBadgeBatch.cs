namespace SIMF.Contracts.Authentication;

/// <summary>
/// One row of the bulk-badge batches list. A persisted
/// record of a single bulk-generate run, so a minted set can be re-emailed or
/// revoked as a unit. Ordered newest-first; a revoked batch keeps its row with
/// <see cref="IsActive"/> = false.
/// </summary>
/// <param name="Id">The batch id.</param>
/// <param name="CountsSummary">Human breakdown of what was minted, e.g. <c>"VIP × 3 + Normal × 2"</c>.</param>
/// <param name="TotalCount">Total badges minted in the batch.</param>
/// <param name="IsDelegate">True when every badge was flagged as a delegation member.</param>
/// <param name="RecipientEmail">The organiser the QR pack was (last) emailed to, if any.</param>
/// <param name="CreatedAt">When the batch was generated (Saudi local).</param>
/// <param name="IsActive">False once the batch has been revoked.</param>
public sealed record AdminBadgeBatchSummary(
    Guid Id,
    string CountsSummary,
    int TotalCount,
    bool IsDelegate,
    string? RecipientEmail,
    DateTime CreatedAt,
    bool IsActive);

/// <summary>
/// Body of <c>POST /admin/visitors/badge-batches/re-email</c>.
/// Re-renders the batch's QR pack and emails it to the given organiser address
/// (a fresh copy; the badges themselves are unchanged).
/// </summary>
public sealed class AdminReEmailBadgeBatchRequest
{
    /// <summary>The batch to re-email.</summary>
    public Guid BatchId { get; set; }

    /// <summary>The organiser email address to send the QR pack to.</summary>
    public string RecipientEmail { get; set; } = string.Empty;
}

/// <summary>Result of a batch re-email: how many badges were
/// packed and whether the mail was enqueued.</summary>
public sealed record AdminReEmailBadgeBatchResponse(int BadgeCount, bool EmailQueued);

/// <summary>
/// Body of <c>POST /admin/visitors/badge-batches/revoke</c>.
/// Disables every account minted by the batch and marks the batch inactive.
/// </summary>
public sealed class AdminRevokeBadgeBatchRequest
{
    /// <summary>The batch to revoke.</summary>
    public Guid BatchId { get; set; }
}

/// <summary>Result of a revoke: how many member accounts were
/// disabled.</summary>
public sealed record AdminRevokeBadgeBatchResponse(int RevokedCount);
