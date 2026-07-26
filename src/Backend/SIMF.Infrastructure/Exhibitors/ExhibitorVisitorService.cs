// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
// Tests: SIMF.Api.Tests/ExhibitorLeadEmailTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibitors;

/// <summary>
/// D-426 — exhibitor lead capture. Resolves a visitor by their entry-badge QR,
/// records the capture, and projects the visitor's full card live from the
/// App-DB <c>UserProfile</c> (+ Organisation / Country) and a permitted email
/// round-trip on the Identity DB (D-157 — bare-Guid logical FKs, no join, no PII
/// snapshot). Only non-visitor ("Other") profile types may use this; a
/// visitor-tier caller is rejected with 403.
///
/// <para>BUG-024 — a NEW capture also emails the lead to the exhibitor's own
/// account address (the owner's "send to exhibitor email" requirement), through
/// the shared template resolver + email queue. A repeat scan is still idempotent
/// and sends nothing, and a mail failure never fails the scan (the queue's
/// log-and-audit contract).</para>
/// </summary>
internal sealed class ExhibitorVisitorService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    IEmailTemplateResolver emailTemplates,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    ILogger<ExhibitorVisitorService> logger,
    TimeProvider timeProvider) : IExhibitorVisitorService
{
    private const int NoteMaxLength = 512;

    // BUG-024 — placeholders for a lead field the visitor's profile leaves empty,
    // one per body language (the token bag feeds both blocks of the one message).
    private const string NotProvidedEn = "Not provided";
    private const string NotProvidedAr = "غير محدد";
    private const string NoNote = "-";

    public async Task<VisitorCard> ScanByBadgeAsync(
        Guid exhibitorUserId, string qrId, string? note,
        CancellationToken cancellationToken = default)
    {
        await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        var normalised = (qrId ?? string.Empty).Trim().ToUpperInvariant();
        if (normalised.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "A badge code is required.",
                "رمز البطاقة مطلوب.");
        }

        var visitorId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.QrId == normalised)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (visitorId is null)
        {
            throw new ApiException(ErrorCodes.NotFound, 404,
                "No visitor badge matches this code.",
                "لا توجد بطاقة زائر مطابقة لهذا الرمز.");
        }
        if (visitorId.Value == exhibitorUserId)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "You cannot scan your own badge.",
                "لا يمكنك مسح بطاقتك الخاصة.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is { Length: > NoteMaxLength })
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                $"Note must be {NoteMaxLength} characters or fewer.",
                $"يجب ألا تتجاوز الملاحظة {NoteMaxLength} حرفاً.");
        }

        // Idempotent per (exhibitor, visitor) — a repeat scan refreshes the note.
        var existing = await appDbContext.ExhibitorVisitorScans
            .FirstOrDefaultAsync(
                s => s.ExhibitorUserId == exhibitorUserId
                    && s.VisitorUserId == visitorId.Value
                    && s.IsActive,
                cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (existing is not null)
        {
            existing.Note = trimmed;
            existing.UpdatedAt = now;
        }
        else
        {
            appDbContext.ExhibitorVisitorScans.Add(new ExhibitorVisitorScan
            {
                Id = Guid.NewGuid(),
                ExhibitorUserId = exhibitorUserId,
                VisitorUserId = visitorId.Value,
                Note = trimmed,
                IsActive = true,
                CreatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        var cards = await ResolveCardsAsync(new[] { visitorId.Value }, cancellationToken);
        var card = cards[visitorId.Value];

        // BUG-024 — only a NEW capture mails the lead out; a repeat scan is a
        // no-op refresh, so the exhibitor is not spammed on every re-scan. The
        // row is already committed, so a mail failure cannot roll the scan back.
        if (existing is null)
        {
            await EmailLeadToExhibitorAsync(exhibitorUserId, card, trimmed, now, cancellationToken);
        }

        return card;
    }

    public async Task<IReadOnlyList<ExhibitorVisitorRow>> ListMyVisitorsAsync(
        Guid exhibitorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        var rows = await appDbContext.ExhibitorVisitorScans
            .AsNoTracking()
            .Where(s => s.ExhibitorUserId == exhibitorUserId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.VisitorUserId, s.Note, s.CreatedAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Array.Empty<ExhibitorVisitorRow>();
        }

        var cards = await ResolveCardsAsync(
            rows.Select(r => r.VisitorUserId).Distinct().ToList(), cancellationToken);

        return rows
            .Select(r => new ExhibitorVisitorRow(
                r.Id, r.CreatedAt, r.Note, cards[r.VisitorUserId]))
            .ToList();
    }

    /// <summary>BUG-024 — emails the captured lead to the exhibitor's own account
    /// address. Fire-and-forget by contract: the capture row is already committed,
    /// so an exhibitor with no account email is logged and skipped, and an enqueue
    /// failure is swallowed + audited by <see cref="EmailQueueExtensions.TryEnqueueAsync"/>
    /// — the 200 and the lead row stand either way. The national ID (encrypted at
    /// rest) and the raw badge QR id are deliberately NOT in the message.</summary>
    private async Task EmailLeadToExhibitorAsync(
        Guid exhibitorUserId, VisitorCard card, string? note,
        DateTimeOffset scannedAt, CancellationToken cancellationToken)
    {
        var recipient = await userDirectory.GetEmailAsync(exhibitorUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            logger.LogWarning(
                "Exhibitor {ExhibitorUserId} has no account email; the lead-capture email was skipped.",
                exhibitorUserId);
            return;
        }

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VisitorName"] = FirstFilled(card.Name, card.NameArabic, NotProvidedEn),
            ["VisitorNameArabic"] = FirstFilled(card.NameArabic, card.Name, NotProvidedAr),
            ["JobTitle"] = FirstFilled(card.JobTitle, card.JobTitleArabic, NotProvidedEn),
            ["JobTitleArabic"] = FirstFilled(card.JobTitleArabic, card.JobTitle, NotProvidedAr),
            ["Organisation"] = FirstFilled(card.Organisation, card.OrganisationArabic, NotProvidedEn),
            ["OrganisationArabic"] =
                FirstFilled(card.OrganisationArabic, card.Organisation, NotProvidedAr),
            // D-219 — Saudi wall clock, 12-hour. No user-facing UTC.
            ["ScannedAt"] = scannedAt.FormatSaudi(),
            ["Note"] = string.IsNullOrWhiteSpace(note) ? NoNote : note,
        };

        var message = await emailTemplates.RenderAsync(
            EmailTemplateType.ExhibitorLeadCapture, recipient, tokens, cancellationToken);
        await emailQueue.TryEnqueueAsync(
            message,
            purpose: "ExhibitorLeadCapture",
            subjectEmail: recipient,
            subjectUserId: exhibitorUserId,
            auditLog: auditLog,
            logger: logger,
            cancellationToken: cancellationToken);
    }

    /// <summary>The first non-blank of the preferred value, its other-language
    /// twin, then the language's "not provided" placeholder.</summary>
    private static string FirstFilled(string? preferred, string? fallback, string placeholder)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }
        return string.IsNullOrWhiteSpace(fallback) ? placeholder : fallback;
    }

    /// <summary>403 unless the caller has a non-visitor ("Other") profile type.
    /// A visitor-tier account (or one with no/unknown profile type) cannot
    /// capture leads.</summary>
    private async Task EnsureExhibitorAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isVisitor = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ProfileType == null || p.ProfileType.IsForVisitor)
            .FirstOrDefaultAsync(cancellationToken);
        // No profile row → FirstOrDefault returns false (default bool); a missing
        // profile is not an exhibitor either, so reject.
        var hasProfile = await appDbContext.UserProfiles
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);
        if (!hasProfile || isVisitor)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "Only exhibitor accounts can scan visitor badges.",
                "مسح بطاقات الزوار متاح لحسابات العارضين فقط.");
        }
    }

    /// <summary>Batch-resolves visitor cards from profiles + org / country lookups
    /// (App DB) and a single email round-trip (Identity DB). A subject with no
    /// profile resolves to an unavailable card. Mirrors the contact-share card
    /// projection (D-284) — kept local to avoid coupling the two services.</summary>
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
}
