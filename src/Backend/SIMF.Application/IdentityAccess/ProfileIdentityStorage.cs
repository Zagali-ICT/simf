// Tests: SIMF.Api.Tests/UserProfileTests.cs (a non-Saudi supplying BOTH an Iqama
//        and a passport persists BOTH documents; the upsert round-trip still
//        answers every shipped wire key)
//        SIMF.Api.Tests/UserProfileIdentifierUniquenessTests.cs (the single
//        digest index catches a CROSS-KIND duplicate)
using SIMF.Application.Abstractions;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The write side of the collapsed identity-document storage — the one place
/// that turns a registrant's typed document numbers into the rows that now hold
/// them.
///
/// <para>It exists as its own class, and not as a private method on
/// <c>UserProfileService</c>, because there are two write paths and they must
/// produce byte-identical storage: the self-service upsert
/// (<c>UserProfileService</c>, in this assembly) and the walk-in desk
/// (<c>AdminAccountService</c>, in Infrastructure, which also carries the offline
/// badge upload). A second copy of the sync rule is exactly how the digest a
/// unique index enforces and the digest a soft guard queries come to
/// disagree.</para>
///
/// <para><b>What this deliberately does NOT do.</b> It does not decide WHICH
/// documents a registrant may hold — the partition between a Saudi's national id
/// and a non-Saudi's Iqama and/or passport stays with the callers, which is why
/// the child rows this writes mirror the superseded columns exactly and the read
/// path is unchanged on the wire.</para>
/// </summary>
public static class ProfileIdentityStorage
{
    /// <summary>
    /// Brings <see cref="UserProfile.IdentityDocuments"/> into line with the three
    /// numbers supplied, adding what is new, re-hashing what changed and removing
    /// what the registrant no longer claims.
    ///
    /// <para>Rows are REMOVED rather than soft-deleted, which is the opposite of
    /// this codebase's usual <c>IsActive = false</c>. A soft-deleted row keeps its
    /// digest in the unique index, so a registrant who corrected a mistyped
    /// passport would be permanently unable to register the corrected one — the
    /// index would still be holding the typo against them.</para>
    ///
    /// <para>The caller must have loaded the collection; an unloaded navigation
    /// looks empty and would silently insert a duplicate of every document.</para>
    /// </summary>
    public static void SyncDocuments(
        UserProfile profile,
        IPiiEncryptor pii,
        string? nationalId,
        string? iqamaNumber,
        string? passportNumber)
    {
        Apply(profile, pii, IdentityDocumentKind.NationalId, nationalId);
        Apply(profile, pii, IdentityDocumentKind.Iqama, iqamaNumber);
        Apply(profile, pii, IdentityDocumentKind.Passport, passportNumber);
    }

    private static void Apply(
        UserProfile profile, IPiiEncryptor pii, IdentityDocumentKind kind, string? number)
    {
        var existing = profile.IdentityDocuments
            .FirstOrDefault(document => document.Kind == kind);

        if (string.IsNullOrWhiteSpace(number))
        {
            if (existing is not null)
            {
                profile.IdentityDocuments.Remove(existing);
            }
            return;
        }

        var trimmed = number.Trim();
        // BlindIndex returns null only for a null/empty input, which the guard
        // above has already excluded, so the digest is always present on a row
        // that exists — which is why the column is required.
        var hash = pii.BlindIndex(trimmed) ?? string.Empty;

        if (existing is null)
        {
            profile.IdentityDocuments.Add(new ProfileIdentityDocument
            {
                // Id and ProfileId are DELIBERATELY left unset, and that is
                // load-bearing rather than laziness. When DetectChanges finds a new
                // instance in a TRACKED parent's navigation, it chooses Added or
                // Modified by whether the key is already set: a populated key means
                // "this row exists", so EF emits an UPDATE ... WHERE Id = x against a
                // row that was never inserted, the statement affects zero rows, and
                // the save dies with a DbUpdateConcurrencyException. Leaving the key
                // at its default is what marks the row Added; relationship fixup
                // fills ProfileId from the principal, on the first save and every
                // save after it.
                Kind = kind,
                Number = trimmed,
                NumberHash = hash,
            });
            return;
        }

        existing.Number = trimmed;
        existing.NumberHash = hash;
    }

    /// <summary>The registrant's document of one kind, or null when they hold
    /// none. Reads the loaded collection rather than querying, so a freshly
    /// upserted profile answers from the rows just written.</summary>
    public static string? DocumentNumber(UserProfile profile, IdentityDocumentKind kind) =>
        profile.IdentityDocuments
            .FirstOrDefault(document => document.Kind == kind)?.Number;
}
