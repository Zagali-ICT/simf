namespace SIMF.Contracts.Badges;

/// <summary>
/// One batch of registrations taken at an OFFLINE badge desk, uploaded
/// once the desk is back on the network.
///
/// <para>The desk has already printed and handed over the badges, so this is a
/// reconciliation upload, not a request to create something new: every item
/// carries the sequence that is already encrypted into the QR in the visitor's
/// hand, and the server stores the account under the QR id that sequence maps
/// to (<c>OfflineBadgeId</c>).</para>
/// </summary>
public sealed class OfflineBadgeBatchRequest
{
    /// <summary>Free-text desk label kept on the audit row, e.g. "Desk 3 — north
    /// entrance". Not used to route anything; the desk's identity is already in
    /// the sequence range it was issued.</summary>
    public string? DeskLabel { get; set; }

    /// <summary>The registrations to reconcile. A batch is processed item by
    /// item — one bad row is reported and the rest still land.</summary>
    public IList<OfflineBadgeRegistration> Registrations { get; set; }
        = new List<OfflineBadgeRegistration>();
}

/// <summary>One badge printed offline.</summary>
public sealed class OfflineBadgeRegistration
{
    /// <summary>The desk-minted sequence encrypted into the printed QR. This is
    /// the item's identity: re-uploading the same sequence is a no-op, which is
    /// what makes a retried upload safe.</summary>
    public long Sequence { get; set; }

    /// <summary>The attendee id the desk minted, which is what the printed QR
    /// carries. The server creates the attendee record under this id so the
    /// badge already in someone's hand resolves the moment the shift uploads.
    ///
    /// <para>Empty from a desk that predates this, in which case the server
    /// mints one and the printed badge resolves through its serial instead.</para>
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>The profile-type code printed on the badge
    /// (<c>ProfileType.Code</c>), not its Guid — the desk works from the small
    /// number the QR carries, and sending the same value lets the server verify
    /// the badge and the record agree.</summary>
    public short ProfileTypeCode { get; set; }

    /// <summary>The name captured at the desk, in whichever script was written.
    /// Mirrored into both name columns when only one was taken.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional second-script name, when the desk captured both.</summary>
    public string? NameArabic { get; set; }

    public string? SaudiMobile { get; set; }

    public string? InternationalMobile { get; set; }

    public string? NationalId { get; set; }

    public string? IqamaNumber { get; set; }

    public string? PassportNumber { get; set; }

    /// <summary>Optional; only ever supplied when the visitor gave one.</summary>
    public string? Email { get; set; }

    /// <summary>When the desk took the registration. Recorded for the
    /// reconciliation report; it does not change how the row is written.</summary>
    public DateTime? RegisteredAt { get; set; }
}

/// <summary>What happened to one uploaded registration.</summary>
public enum OfflineBadgeUploadStatus
{
    /// <summary>Account created and approved; the printed badge works.</summary>
    Created = 0,

    /// <summary>Account created, but auto-approval is not armed, so it is sitting
    /// in the pending queue and the printed badge will be refused until an admin
    /// approves it. Deliberately reported rather than silently forced through:
    /// approval is its own switch.</summary>
    CreatedPendingApproval = 1,

    /// <summary>This sequence was uploaded before. Nothing was changed.</summary>
    AlreadyUploaded = 2,

    /// <summary>Not written. <see cref="OfflineBadgeUploadResult.ErrorCode"/>
    /// names why — a duplicate identity document, an unknown profile-type code,
    /// or a sequence outside the accepted range.</summary>
    Rejected = 3,
}

/// <summary>Per-item outcome, so the desk can reprint or chase exactly
/// the rows that did not land.</summary>
public sealed record OfflineBadgeUploadResult(
    long Sequence,
    string QrId,
    OfflineBadgeUploadStatus Status,
    string? ErrorCode,
    string? Message);

/// <summary>The reconciliation report for one uploaded batch.</summary>
public sealed record OfflineBadgeBatchResponse(
    int Submitted,
    int Created,
    int PendingApproval,
    int AlreadyUploaded,
    int Rejected,
    IReadOnlyList<OfflineBadgeUploadResult> Results);
