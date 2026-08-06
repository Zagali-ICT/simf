using SIMF.Contracts.Contacts;

namespace SIMF.Application.Contacts.Abstractions;

/// <summary>
/// SIMF-FDS-014 §5.7 (D-284, Track 2) — visitor-to-visitor contact sharing.
/// All operations are caller-scoped (the visitor's own <c>SimfUser.Id</c> from
/// the JWT <c>sub</c>). Cards are projected live; nothing of the subject's PII
/// is persisted on save.
/// </summary>
public interface IVisitorShareService
{
    /// <summary>Returns the caller's active share token, minting one on first
    /// request. Idempotent — repeated calls return the same active token.</summary>
    Task<VisitorShareTokenResponse> GetOrMintTokenAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Revokes the caller's active token and mints a fresh one so a
    /// previously shared code stops resolving.</summary>
    Task<VisitorShareTokenResponse> RotateTokenAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a share token to the owner's live contact card.
    /// Throws 404 when the token is unknown / revoked.</summary>
    Task<VisitorCard> ResolveAsync(
        string token, CancellationToken cancellationToken = default);

    /// <summary>Saves the holder of <paramref name="token"/> to the caller's
    /// <em>My Contacts</em>. Idempotent per (owner, subject); updates the note
    /// on a repeat save. Throws 404 (bad token) / 400 (saving yourself).</summary>
    Task<SavedContactRow> SaveAsync(
        Guid ownerUserId, string token, string? note,
        CancellationToken cancellationToken = default);

    /// <summary>The caller's saved contacts, each card resolved on read.</summary>
    Task<IReadOnlyList<SavedContactRow>> ListSavedAsync(
        Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes one of the caller's saved contacts. Idempotent;
    /// throws 404 when the id is not the caller's.</summary>
    Task RemoveSavedAsync(
        Guid ownerUserId, Guid savedContactId,
        CancellationToken cancellationToken = default);

    /// <summary>The live card for one of the caller's saved contacts (used by
    /// the vCard export). Throws 404 when the id is not the caller's.</summary>
    Task<VisitorCard> GetSavedCardAsync(
        Guid ownerUserId, Guid savedContactId,
        CancellationToken cancellationToken = default);
}
