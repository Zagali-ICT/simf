using SIMF.Contracts.Badges;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Reconciles a batch of badges printed at an OFFLINE desk back into the
/// system once the desk is on the network again.
/// </summary>
public interface IOfflineBadgeUploadService
{
    /// <summary>
    /// Writes each registration in the batch, keyed by the sequence already
    /// encrypted into the printed QR.
    ///
    /// <para>Processed ITEM BY ITEM, not as one transaction. During a rush an
    /// upload is the only record that a badge was handed out, so one duplicated
    /// identity document must not discard the ninety-nine rows around it. Each
    /// item's outcome is reported so the desk can chase exactly what failed.</para>
    /// </summary>
    Task<OfflineBadgeBatchResponse> UploadAsync(
        Guid actorUserId,
        OfflineBadgeBatchRequest request,
        CancellationToken cancellationToken = default);
}
