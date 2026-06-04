// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Contacts.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Contacts;
using SIMF.Domain.Contacts;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Contacts;

/// <summary>
/// SIMF-FDS-014 §5.4–5.7 (D-284, Track 2) — visitor-to-visitor contact sharing.
/// Mints / rotates a visitor's dedicated share token, resolves a token to a live
/// card projected from the owner's <c>UserProfile</c> (+ Organisation / Country
/// lookups on the App DB + a permitted email round-trip on the Identity DB,
/// OI-2), and manages the caller's <em>My Contacts</em>. Cross-DB references are
/// bare-Guid logical FKs resolved on read — no EF join, no PII snapshot (D-157).
/// </summary>
internal sealed class VisitorShareService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
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
        var existing = await appDbContext.VisitorShareTokens
            .AsNoTracking()
            .Where(token => token.UserId == userId && token.IsActive)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => token.Token)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return new VisitorShareTokenResponse(existing);
        }

        var minted = await MintUniqueTokenAsync(cancellationToken);
        appDbContext.VisitorShareTokens.Add(new VisitorShareToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = minted,
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken);
        return new VisitorShareTokenResponse(minted);
    }

    public async Task<VisitorShareTokenResponse> RotateTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var actives = await appDbContext.VisitorShareTokens
            .Where(token => token.UserId == userId && token.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var token in actives)
        {
            token.IsActive = false;
            token.RevokedAt = now;
        }

        var minted = await MintUniqueTokenAsync(cancellationToken);
        appDbContext.VisitorShareTokens.Add(new VisitorShareToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = minted,
            IsActive = true,
            CreatedAt = now,
        });
        await appDbContext.SaveChangesAsync(cancellationToken);
        return new VisitorShareTokenResponse(minted);
    }

    public async Task<VisitorCard> ResolveAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var normalised = (token ?? string.Empty).Trim().ToUpperInvariant();
        var owner = await appDbContext.VisitorShareTokens
            .AsNoTracking()
            .Where(row => row.Token == normalised && row.IsActive)
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
            existing.UpdatedAt = timeProvider.GetUtcNow();
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
            CreatedAt = timeProvider.GetUtcNow(),
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
                    card.JobTitle, card.Organisation, r.Note, r.CreatedAt,
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
        saved.UpdatedAt = timeProvider.GetUtcNow();
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

        var profiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new
            {
                p.UserId,
                p.Name,
                p.NameArabic,
                p.JobTitle,
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

        // Email is Identity-owned — one cross-DB round-trip (D-157: no join).
        var emails = (await identityDbContext.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email })
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.Id, u => u.Email);

        foreach (var userId in userIds.Distinct())
        {
            var profile = profiles.FirstOrDefault(p => p.UserId == userId);
            if (profile is null)
            {
                result[userId] = new VisitorCard(
                    userId, string.Empty, string.Empty, null, null, null,
                    null, null, null, null, null, null, Available: false);
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
                orgEn, orgAr, email, profile.SaudiMobile, profile.InternationalMobile,
                countryId, countryEn, countryAr, Available: true);
        }

        return result;
    }

    private static SavedContactRow ToRow(SavedContact saved, VisitorCard card) =>
        new(saved.Id, saved.SubjectUserId, card.Name, card.NameArabic,
            card.JobTitle, card.Organisation, saved.Note, saved.CreatedAt,
            card.Available);

    // -- Token minting (Crockford base32; uniqueness-checked) -----------------

    private async Task<string> MintUniqueTokenAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxMintAttempts; attempt++)
        {
            var candidate = GenerateToken();
            var clash = await appDbContext.VisitorShareTokens
                .AsNoTracking()
                .AnyAsync(t => t.Token == candidate, cancellationToken);
            if (!clash)
            {
                return candidate;
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
