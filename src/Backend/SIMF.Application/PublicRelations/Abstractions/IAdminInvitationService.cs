using SIMF.Common;
using SIMF.Contracts.Admin;

namespace SIMF.Application.PublicRelations.Abstractions;

/// <summary>
/// D-168 (gap doc G5, PDF §2.7.3) — public-relations service surface.
/// Owns admin CRUD over <c>Invitation</c> rows plus the VIP-list view
/// and the bulk-notify-VIPs dispatcher. Used by both Administrator and
/// <c>PublicRelations</c> roles — the endpoint policy gates the surface.
/// </summary>
public interface IAdminInvitationService
{
    Task<GridPage<AdminInvitationSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    Task<AdminInvitationDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task<AdminInvitationDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminInvitationDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>D-168 — VIP list. Returns every <c>UserProfile</c> whose
    /// <c>ProfileType.Name</c> is in <c>{VVIP, VIP, Gold}</c> (PDF §2.7.3
    /// + gap doc §G5 step 4). Server-paged via <see cref="GridQuery"/>.</summary>
    Task<GridPage<AdminVipSummary>> ListVipsAsync(
        GridQuery query, CancellationToken cancellationToken = default);

    /// <summary>D-168 — bulk dispatch a message to one or more VIPs. The
    /// recipient set is validated against the VIP list — non-VIP ids are
    /// skipped (recorded on the result). Each recipient gets one in-app
    /// row and one queued email; recipients without an email get the
    /// in-app row only.</summary>
    Task<AdminNotifyVipsResult> NotifyVipsAsync(
        Guid actorUserId,
        AdminNotifyVipsRequest request,
        CancellationToken cancellationToken = default);
}
