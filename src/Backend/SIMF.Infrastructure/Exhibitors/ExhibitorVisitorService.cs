// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
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
/// snapshot). DEF-EXH-001: only a genuine exhibitor (a profile type carrying
/// <see cref="MobileAppRole.Exhibitor"/>, D-519) may use this; every other
/// caller is rejected with 403. DEF-EXH-003: the scanned subject must itself be
/// an active audience-side account. DEF-EXH-002: a new capture notifies the
/// visitor, naming the exhibitor their card was shared with.
/// </summary>
internal sealed class ExhibitorVisitorService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    ILogger<ExhibitorVisitorService> logger) : IExhibitorVisitorService
{
    private const int NoteMaxLength = 512;

    public async Task<VisitorCard> ScanByBadgeAsync(
        Guid exhibitorUserId, string qrId, string? note,
        CancellationToken cancellationToken = default)
    {
        var exhibitor = await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        var normalised = (qrId ?? string.Empty).Trim().ToUpperInvariant();
        if (normalised.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "A badge code is required.",
                "رمز البطاقة مطلوب.");
        }

        // DEF-EXH-003 — the SUBJECT must be eligible too, not just the caller: an
        // ACTIVE profile that is not a partner-side (IsForVisitor=false) type, so a
        // staff badge or another exhibitor's badge is never capturable as a "lead".
        // A visitor with no tier assigned yet stays eligible — the approve-time tier
        // is optional (AdminAccountService.Approval, CS-D / D-386), so a null
        // ProfileType is an ordinary audience account, not a partner. An ineligible
        // badge returns the same 404 as an unknown one — the caller never learns
        // whether the code exists.
        var visitorId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.QrId == normalised
                && p.IsActive
                && (p.ProfileType == null || p.ProfileType.IsForVisitor))
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
        if (existing is not null)
        {
            existing.Note = trimmed;
            existing.UpdatedAt = timeProvider.GetUtcNow();
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
                CreatedAt = timeProvider.GetUtcNow(),
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        if (existing is null)
        {
            await NotifyVisitorCapturedAsync(visitorId.Value, exhibitor, cancellationToken);
        }

        var cards = await ResolveCardsAsync(new[] { visitorId.Value }, cancellationToken);
        return cards[visitorId.Value];
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

    /// <summary>DEF-EXH-001 — 403 unless the caller is a genuine EXHIBITOR: an
    /// active profile whose assigned profile type carries
    /// <see cref="MobileAppRole.Exhibitor"/> (D-519). The former test admitted any
    /// profile type that merely was NOT a visitor type, so Staff / Moderator /
    /// Media / Sponsor tokens could call the scan + list endpoints and harvest
    /// visitor PII (login email and both mobile numbers).
    ///
    /// <para>No cross-database work (D-157): <c>ProfileType</c> lives on the App DB
    /// beside <c>UserProfile</c>, and <c>MobileAppRole</c> is the same column the
    /// JWT's app role is resolved from
    /// (<c>UserProfileRepository.GetAssignedProfileTypeRoleAsync</c>).</para>
    ///
    /// <para>Returns the exhibitor's display names for the subject notification
    /// (DEF-EXH-002).</para></summary>
    private async Task<ExhibitorIdentity> EnsureExhibitorAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var exhibitor = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId
                && p.IsActive
                && p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor)
            .Select(p => new ExhibitorIdentity(p.Name, p.NameArabic))
            .FirstOrDefaultAsync(cancellationToken);
        if (exhibitor is null)
        {
            throw new ApiException(ErrorCodes.Forbidden, 403,
                "Only exhibitor accounts can scan visitor badges.",
                "مسح بطاقات الزوار متاح لحسابات العارضين فقط.");
        }
        return exhibitor;
    }

    /// <summary>DEF-EXH-002 — tell the visitor, in-app, that their contact card
    /// was shared with the NAMED exhibitor who scanned their entry badge. Raised
    /// once per new capture only; the idempotent re-scan path (which merely
    /// refreshes the note) stays silent. Best-effort like the other request /
    /// booking flows — a dispatch failure never undoes the committed capture.
    /// Notifications live on the Identity DB, dispatched through its own unit of
    /// work, so this is not a cross-database transaction (D-157).</summary>
    private Task NotifyVisitorCapturedAsync(
        Guid visitorUserId, ExhibitorIdentity exhibitor,
        CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(exhibitor.Name)
            ? "An exhibitor"
            : exhibitor.Name.Trim();
        var nameArabic = string.IsNullOrWhiteSpace(exhibitor.NameArabic)
            ? "أحد العارضين"
            : exhibitor.NameArabic.Trim();
        return notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = visitorUserId,
            Kind = NotificationKind.ExhibitorLeadCaptured,
            Title = "Your details were shared with an exhibitor",
            TitleArabic = "تمت مشاركة بياناتك مع أحد العارضين",
            Body = $"{name} scanned your entry badge, so your contact card "
                + "(name, job title, organisation, email and mobile) was shared with them.",
            BodyArabic = $"قام {nameArabic} بمسح بطاقة دخولك، وتمت مشاركة بطاقة "
                + "التواصل الخاصة بك (الاسم والمسمى الوظيفي والجهة والبريد الإلكتروني والجوال) معه.",
            Severity = NotificationSeverity.Info,
            SendEmail = false,
        }, logger, cancellationToken);
    }

    /// <summary>The scanning exhibitor's bilingual display name — carried from the
    /// authorisation query into the subject notification so the visitor is told WHO
    /// their card went to.</summary>
    private sealed record ExhibitorIdentity(string Name, string NameArabic);

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
