// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Abstractions;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Contacts;
using SIMF.Domain.Contacts;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Contacts;

/// <summary>
/// Visitor-to-visitor contact sharing.
/// Mints / rotates a visitor's dedicated share token, resolves a token to a live
/// card projected from the owner's <c>UserProfile</c> (+ Organisation / Country
/// lookups on the App DB + a permitted email round-trip on the Identity DB),
/// and manages the caller's <em>My Contacts</em>. Cross-DB references are
/// bare-Guid logical FKs resolved on read — no EF join, no PII snapshot.
///
/// <para>The share code is a replayable credential — it resolves to a card the
/// holder is not otherwise entitled to — so it is encrypted at rest here and
/// looked up by keyed digest. The encryption is applied in this service rather
/// than by a value converter because a converter needs the encryptor injected
/// into the DbContext, and an entity configuration discovered by
/// <c>ApplyConfigurationsFromAssembly</c> is constructed without dependencies.
/// This is the only reader of the table, so there is no second path that could
/// see the raw column and mistake ciphertext for a code.</para>
/// </summary>
internal sealed class VisitorShareService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    IPiiEncryptor pii,
    TimeProvider timeProvider) : IVisitorShareService
{
    // Crockford base32 (excludes I, L, O, U, 0, 1) — mirrors the QrId minter.
    private const string TokenAlphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int TokenLength = 12;
    private const int MaxMintAttempts = 8;
    private const int NoteMaxLength = 512;

    public async Task<VisitorShareTokenResponse> GetOrMintTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var stored = await appDbContext.VisitorShareTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId && token.IsActive)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => token.Token)
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is not null)
        {
            // The owner is entitled to read their own code back, which is why this
            // column is encrypted and not hashed.
            return new VisitorShareTokenResponse(pii.Decrypt(stored)!);
        }

        var minted = await MintAndAddAsync(userId, timeProvider.SimfNow(), cancellationToken);
        await appDbContext.SaveChangesAsync(cancellationToken);
        return new VisitorShareTokenResponse(minted);
    }

    public async Task<VisitorShareTokenResponse> RotateTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
        var actives = await appDbContext.VisitorShareTokens
            .Where(token => token.UserId == userId && token.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var token in actives)
        {
            token.IsActive = false;
            token.RevokedAt = now;
        }

        var minted = await MintAndAddAsync(userId, now, cancellationToken);
        await appDbContext.SaveChangesAsync(cancellationToken);
        return new VisitorShareTokenResponse(minted);
    }

    public async Task<VisitorCard> ResolveAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var normalised = (token ?? string.Empty).Trim().ToUpperInvariant();
        // An empty code has no digest to match, and asking the database for one
        // would compare a required column against NULL rather than answering.
        var digest = normalised.Length == 0 ? null : pii.BlindIndex(normalised);
        var owner = digest is null ? null : await appDbContext.VisitorShareTokens
            .AsNoTracking()
            .Where(row => row.TokenHash == digest && row.IsActive)
            .Select(row => (Guid?)row.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            throw new ApiException(ErrorCodes.NotFound, 404,
                "Share code not found or no longer valid.",
                "رمز المشاركة غير موجود أو لم يعد صالحاً.");
        }

        var cards = await ResolveCardsAsync(new[] { owner.Value }, cancellationToken);
        return cards[owner.Value];
    }

    public async Task<SavedContactRow> SaveAsync(
        Guid ownerUserId, string token, string? note,
        CancellationToken cancellationToken = default)
    {
        var card = await ResolveAsync(token, cancellationToken); // 404 on bad token
        if (card.UserId == ownerUserId)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "You cannot save your own contact.",
                "لا يمكنك حفظ جهة اتصالك الخاصة.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > NoteMaxLength })
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"Note must be {NoteMaxLength} characters or fewer.",
                $"يجب ألا تتجاوز الملاحظة {NoteMaxLength} حرفاً.");
        }

        // Idempotent per (owner, subject) — a repeat save just refreshes the note.
        var existing = await appDbContext.SavedContacts
            .FirstOrDefaultAsync(
                s => s.OwnerUserId == ownerUserId
                    && s.SubjectUserId == card.UserId
                    && s.IsActive,
                cancellationToken);
        if (existing is not null)
        {
            existing.Note = trimmed;
            existing.UpdatedAt = timeProvider.SimfNow();
            await appDbContext.SaveChangesAsync(cancellationToken);
            return ToRow(existing, card);
        }

        var saved = new SavedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            SubjectUserId = card.UserId,
            Note = trimmed,
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        appDbContext.SavedContacts.Add(saved);
        await appDbContext.SaveChangesAsync(cancellationToken);
        return ToRow(saved, card);
    }

    public async Task<IReadOnlyList<SavedContactRow>> ListSavedAsync(
        Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        var rows = await appDbContext.SavedContacts
            .AsNoTracking()
            .Where(s => s.OwnerUserId == ownerUserId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.SubjectUserId, s.Note, s.CreatedAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Array.Empty<SavedContactRow>();
        }

        var cards = await ResolveCardsAsync(
            rows.Select(r => r.SubjectUserId).Distinct().ToList(), cancellationToken);

        return rows
            .Select(r =>
            {
                var card = cards[r.SubjectUserId];
                return new SavedContactRow(
                    r.Id, r.SubjectUserId, card.Name, card.NameArabic,
                    card.JobTitle, card.JobTitleArabic, card.Organisation, r.Note, r.CreatedAt,
                    card.Available);
            })
            .ToList();
    }

    public async Task RemoveSavedAsync(
        Guid ownerUserId, Guid savedContactId,
        CancellationToken cancellationToken = default)
    {
        var saved = await appDbContext.SavedContacts
            .FirstOrDefaultAsync(
                s => s.Id == savedContactId && s.OwnerUserId == ownerUserId,
                cancellationToken)
            ?? throw new ApiException(ErrorCodes.NotFound, 404,
                "Saved contact not found.",
                "لم يتم العثور على جهة الاتصال المحفوظة.");
        if (!saved.IsActive)
        {
            return; // idempotent
        }
        saved.Deactivate();
        saved.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<VisitorCard> GetSavedCardAsync(
        Guid ownerUserId, Guid savedContactId,
        CancellationToken cancellationToken = default)
    {
        var subjectId = await appDbContext.SavedContacts
            .AsNoTracking()
            .Where(s => s.Id == savedContactId && s.OwnerUserId == ownerUserId && s.IsActive)
            .Select(s => (Guid?)s.SubjectUserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (subjectId is null)
        {
            throw new ApiException(ErrorCodes.NotFound, 404,
                "Saved contact not found.",
                "لم يتم العثور على جهة الاتصال المحفوظة.");
        }
        var cards = await ResolveCardsAsync(new[] { subjectId.Value }, cancellationToken);
        return cards[subjectId.Value];
    }

    // -- Card projection (live; no PII snapshot) ------------------------------

    /// <summary>Batch-resolves visitor cards from their profiles + org / country
    /// lookups (App DB) and a single email round-trip (Identity DB). A subject
    /// with no profile resolves to an unavailable card.</summary>
    private async Task<Dictionary<Guid, VisitorCard>> ResolveCardsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, VisitorCard>();
        if (userIds.Count == 0)
        {
            return result;
        }

        // Every id here came from a share token or a saved-contact row, so it is
        // always an account id; a profile carrying no account can never be one of
        // them, and matching it would need a user id it does not have.
        var profiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId != null && userIds.Contains(p.UserId!.Value))
            .Select(p => new
            {
                p.UserId,
                p.Name,
                p.NameArabic,
                p.JobTitle,
                p.JobTitleArabic,
                p.OrganisationId,
                p.NationalityId,
                p.SaudiMobile,
                p.InternationalMobile,
            })
            .ToListAsync(cancellationToken);

        var orgIds = profiles
            .Where(p => p.OrganisationId.HasValue)
            .Select(p => p.OrganisationId!.Value).Distinct().ToList();
        var orgs = orgIds.Count == 0
            ? new Dictionary<Guid, (string? En, string Ar)>()
            : (await appDbContext.Organisations.AsNoTracking()
                .Where(o => orgIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Name, o.NameArabic })
                .ToListAsync(cancellationToken))
                .ToDictionary(o => o.Id, o => (En: (string?)o.Name, Ar: o.NameArabic));

        var countryIds = profiles
            .Where(p => p.NationalityId > 0)
            .Select(p => p.NationalityId).Distinct().ToList();
        var countries = countryIds.Count == 0
            ? new Dictionary<int, (string En, string Ar)>()
            : (await appDbContext.Countries.AsNoTracking()
                .Where(c => countryIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Name, c.NameArabic })
                .ToListAsync(cancellationToken))
                .ToDictionary(c => c.Id, c => (En: c.Name, Ar: c.NameArabic));

        // Email is Identity-owned — one cross-DB round-trip.
        var emails = await userDirectory.GetEmailsAsync(userIds, cancellationToken);

        foreach (var userId in userIds.Distinct())
        {
            var profile = profiles.FirstOrDefault(p => p.UserId == userId);
            if (profile is null)
            {
                result[userId] = new VisitorCard(
                    userId, string.Empty, string.Empty, null, null, null,
                    null, null, null, null, null, null, null, Available: false);
                continue;
            }

            string? orgEn = null, orgAr = null;
            if (profile.OrganisationId is { } oid && orgs.TryGetValue(oid, out var org))
            {
                orgEn = org.En;
                orgAr = org.Ar;
            }

            int? countryId = profile.NationalityId > 0 ? profile.NationalityId : null;
            string? countryEn = null, countryAr = null;
            if (countryId is { } cid && countries.TryGetValue(cid, out var country))
            {
                countryEn = country.En;
                countryAr = country.Ar;
            }

            emails.TryGetValue(userId, out var email);

            result[userId] = new VisitorCard(
                userId, profile.Name, profile.NameArabic, profile.JobTitle,
                profile.JobTitleArabic,
                orgEn, orgAr, email, profile.SaudiMobile, profile.InternationalMobile,
                countryId, countryEn, countryAr, Available: true);
        }

        return result;
    }

    private static SavedContactRow ToRow(SavedContact saved, VisitorCard card) =>
        new(saved.Id, saved.SubjectUserId, card.Name, card.NameArabic,
            card.JobTitle, card.JobTitleArabic, card.Organisation, saved.Note, saved.CreatedAt,
            card.Available);

    // -- Token minting (Crockford base32; uniqueness-checked) -----------------

    /// <summary>Mints a fresh code, adds its row to the change tracker and returns
    /// the code in the clear for the caller to hand back. The row keeps the
    /// encrypted code and its digest; the caller saves.</summary>
    private async Task<string> MintAndAddAsync(
        Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var (code, digest) = await MintUniqueTokenAsync(cancellationToken);
        appDbContext.VisitorShareTokens.Add(new VisitorShareToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = pii.Encrypt(code)!,
            TokenHash = digest,
            IsActive = true,
            CreatedAt = now,
        });
        return code;
    }

    /// <summary>A code no active or revoked row already carries, with its digest.
    /// The clash check runs against the digest because that is the unique column;
    /// the ciphertext differs on every write and could never collide.</summary>
    private async Task<(string Code, string Digest)> MintUniqueTokenAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxMintAttempts; attempt++)
        {
            var candidate = GenerateToken();
            // BlindIndex returns null only for null/empty, which a generated code
            // never is, so the digest is always present.
            var digest = pii.BlindIndex(candidate)!;
            var clash = await appDbContext.VisitorShareTokens
                .AsNoTracking()
                .AnyAsync(t => t.TokenHash == digest, cancellationToken);
            if (!clash)
            {
                return (candidate, digest);
            }
        }
        throw new InvalidOperationException(
            "Could not mint a unique share token after several attempts.");
    }

    private static string GenerateToken()
    {
        Span<char> buffer = stackalloc char[TokenLength];
        Span<byte> entropy = stackalloc byte[TokenLength];
        RandomNumberGenerator.Fill(entropy);
        for (var i = 0; i < TokenLength; i++)
        {
            buffer[i] = TokenAlphabet[entropy[i] % TokenAlphabet.Length];
        }
        return new string(buffer);
    }
}
