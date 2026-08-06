// Tests: covered via the SIMF.Api.Tests service suites that consume it —
// Speaker/Delegation/BadgeUpdate/ParticipationDocumentRequestsTests (GetEmailAsync),
// AdminGatesTests, AdminInvitationsTests, ExhibitorVisitorScanTests,
// VisitorContactSharingTests (GetDisplayNamesAsync / GetEmailsAsync).
namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Resolves Identity-owned user attributes (email, display name) for App-side
/// services that must reference a user across the physical DB boundary.
/// The implementation queries ONLY <c>SimfIdentityDbContext</c>; callers keep
/// their own <c>SIMF_App</c> query and merge the result in memory — never a
/// cross-database JOIN, never a unit of work spanning both contexts.
/// </summary>
public interface IIdentityUserDirectory
{
    /// <summary>The user's email, or <c>null</c> when the id is unknown.</summary>
    Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Maps each given user id to its display name (empty string when the
    /// user has none). Ids with no matching user are absent from the map.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Maps each given user id to its email (which may itself be
    /// <c>null</c>). Ids with no matching user are absent from the map.</summary>
    Task<IReadOnlyDictionary<Guid, string?>> GetEmailsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
