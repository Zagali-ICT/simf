// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
using System.Linq.Expressions;
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
using SIMF.Domain.Profiles;
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
/// an active audience-side account. DEF-EXH-004: that same subject test also runs
/// on the READ path, so rows captured while the old rule was in force stop
/// projecting a card. DEF-EXH-006: the role is not enough on its own — a CURRENT
/// <see cref="ExhibitorMembership"/> of a live exhibitor is required too, so
/// dropping an officer from a booth revokes their scanning authority.
/// DEF-EXH-002 / DEF-EXH-007: a new capture notifies the visitor, naming the
/// EXHIBITOR their card was shared with.
/// </summary>
internal sealed class ExhibitorVisitorService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    ILogger<ExhibitorVisitorService> logger) : IExhibitorVisitorService
{
    private const int NoteMaxLength = 512;

    /// <summary>DEF-EXH-003 — the SUBJECT eligibility test: an ACTIVE profile that
    /// is not a partner-side (<c>IsForVisitor=false</c>) type, so a staff badge or
    /// another exhibitor's badge is never capturable as a "lead". A visitor with no
    /// tier assigned yet stays eligible — the approve-time tier is optional
    /// (AdminAccountService.Approval, CS-D / D-386), so a null ProfileType is an
    /// ordinary audience account, not a partner.
    ///
    /// <para>DEF-EXH-004 — held as one expression so the capture path and the READ
    /// path apply exactly the same rule and can never drift.</para></summary>
    private static readonly Expression<Func<UserProfile, bool>> IsCapturableSubject =
        profile => profile.IsActive
            && (profile.ProfileType == null || profile.ProfileType.IsForVisitor);

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

        // DEF-EXH-003 — the SUBJECT must be eligible too, not just the caller
        // (see IsCapturableSubject). An ineligible badge returns the same 404 as an
        // unknown one — the caller never learns whether the code exists.
        var visitorId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(IsCapturableSubject)
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

        // DEF-EXH-004 — re-run the capture-time SUBJECT test on the READ path.
        // Capture-time-only enforcement left every row taken while the old rule was
        // in force (no IsActive, no audience-side filter) still projecting a full
        // live card — login email + both mobile numbers — for a staff / rival
        // exhibitor / since-deactivated subject. A subject that is no longer
        // capturable simply drops out of the list, so no PII is projected for it
        // (this also covers a subject whose profile row has gone).
        var subjectIds = rows.Select(r => r.VisitorUserId).Distinct().ToList();
        var eligibleSubjectIds = (await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(IsCapturableSubject)
            .Where(p => subjectIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var visible = rows
            .Where(r => eligibleSubjectIds.Contains(r.VisitorUserId))
            .ToList();
        if (visible.Count == 0)
        {
            return Array.Empty<ExhibitorVisitorRow>();
        }

        var cards = await ResolveCardsAsync(
            visible.Select(r => r.VisitorUserId).Distinct().ToList(), cancellationToken);

        return visible
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
    /// <para>DEF-EXH-006 — the role alone is not authority. It is granted at
    /// provisioning time and then lives on the PERSON's profile, so it outlived
    /// their booth: dropping an officer from an exhibitor left them able to go on
    /// scanning badges and reading contact cards. A CURRENT membership of a live
    /// exhibitor is required alongside the role, so revoking the membership (or
    /// closing the exhibitor) revokes the tools with it.</para>
    ///
    /// <para>No cross-database work (D-157): <c>ProfileType</c>,
    /// <c>ExhibitorMembership</c> and <c>Exhibitor</c> all live on the App DB
    /// beside <c>UserProfile</c>, and <c>MobileAppRole</c> is the same column the
    /// JWT's app role is resolved from
    /// (<c>UserProfileRepository.GetAssignedProfileTypeRoleAsync</c>).</para>
    ///
    /// <para>Returns the booth's display names for the subject notification
    /// (DEF-EXH-002 / DEF-EXH-007).</para></summary>
    private async Task<ExhibitorIdentity> EnsureExhibitorAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var officer = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId
                && p.IsActive
                && p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor)
            .Select(p => new { p.Name, p.NameArabic })
            .FirstOrDefaultAsync(cancellationToken);
        if (officer is null)
        {
            throw NotAnActiveBoothOfficer();
        }

        // DEF-EXH-006 — the membership half. Second query rather than one clever
        // projection: it only runs once the role has already been established, and
        // ExhibitorMembership.UserId carries a filtered unique index on the active
        // rows, so it is a single-row indexed lookup.
        var booth = await appDbContext.ExhibitorMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId
                && m.IsActive
                && m.Exhibitor != null
                && m.Exhibitor.IsActive)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Exhibitor!.Name, m.Exhibitor!.NameArabic })
            .FirstOrDefaultAsync(cancellationToken);
        if (booth is null)
        {
            throw NotAnActiveBoothOfficer();
        }

        // DEF-EXH-007 — name the EXHIBITOR, not the account. A CP-provisioned
        // officer gets a stub UserProfile with no Name / NameArabic at all
        // (AdminAccountService.CreateAccountAsync), so reading the name off the
        // scanning account degraded the capture notice to "An exhibitor" for
        // exactly the accounts the CP creates. The officer's own name is kept as
        // the fallback for a self-registered exhibitor whose booth row somehow
        // carries a blank name.
        return new ExhibitorIdentity(
            string.IsNullOrWhiteSpace(booth.Name) ? officer.Name : booth.Name,
            string.IsNullOrWhiteSpace(booth.NameArabic)
                ? officer.NameArabic
                : booth.NameArabic);
    }

    /// <summary>One 403 for both halves of the exhibitor test (DEF-EXH-001 role,
    /// DEF-EXH-006 current membership) — the caller learns they may not use the
    /// booth tools, not which half of the rule they failed.</summary>
    private static ApiException NotAnActiveBoothOfficer() =>
        new(ErrorCodes.Forbidden, 403,
            "Only exhibitor accounts with a current booth membership can scan visitor badges.",
            "مسح بطاقات الزوار متاح فقط لحسابات العارضين المرتبطة بجناح فعّال.");

    /// <summary>DEF-EXH-002 / DEF-EXH-007 — tell the visitor, in-app, that their
    /// contact card was shared with the NAMED exhibitor whose officer scanned their
    /// entry badge (<see cref="EnsureExhibitorAsync"/> resolves the name). Raised
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

    /// <summary>The bilingual display name of the EXHIBITOR the scanning officer
    /// represents — carried from the authorisation query into the subject
    /// notification so the visitor is told WHO their card went to
    /// (DEF-EXH-007).</summary>
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
