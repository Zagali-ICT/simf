// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
// Tests: SIMF.Api.Tests/ExhibitorLeadEmailTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
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
/// Exhibitor lead capture. Resolves a visitor by their entry-badge QR,
/// records the capture, and projects the visitor's full card live from the
/// App-DB <c>UserProfile</c> (+ Organisation / Country) and a permitted email
/// round-trip on the Identity DB (bare-Guid logical FKs, no join, no PII
/// snapshot). Only a genuine exhibitor (a profile type carrying
/// <see cref="MobileAppRole.Exhibitor"/>) may use this; every other
/// caller is rejected with 403. The scanned subject only has to be an ACTIVE
/// account holding a badge, whatever its profile type. That same subject test also
/// runs on the READ path, so a since-deactivated subject stops projecting a card.
/// The role is not enough on its own — a CURRENT
/// <see cref="ExhibitorMembership"/> of a live exhibitor is required too, so
/// dropping an officer from a booth revokes their scanning authority.
/// A new capture notifies the visitor, naming the
/// EXHIBITOR their card was shared with.
///
/// <para>A NEW capture also emails the lead to the exhibitor's own
/// account address (the owner's "send to exhibitor email" requirement), through
/// the shared template resolver + email queue. A repeat scan is still idempotent
/// and sends nothing, and a mail failure never fails the scan (the queue's
/// log-and-audit contract).</para>
/// </summary>
internal sealed class ExhibitorVisitorService(
    SimfAppDbContext appDbContext,
    IIdentityUserDirectory userDirectory,
    TimeProvider timeProvider,
    INotificationDispatcher notifications,
    IEmailTemplateResolver emailTemplates,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    IQrResolver qrResolver,
    ILogger<ExhibitorVisitorService> logger) : IExhibitorVisitorService
{
    private const int NoteMaxLength = 512;

    // Placeholders for a lead field the visitor's profile leaves empty,
    // one per body language (the token bag feeds both blocks of the one message).
    private const string NotProvidedEn = "Not provided";
    private const string NotProvidedAr = "غير محدد";
    private const string NoNote = "-";

    /// <summary>The SUBJECT eligibility test: an ACTIVE profile — i.e. any live
    /// account holding a badge, whatever profile type it carries.
    ///
    /// <para><b>The owner's ruling is "can scan all badges".</b> The
    /// owner was asked directly whether a booth may capture a MEDIA or SPONSOR
    /// attendee's badge and ruled that ALL badges are scannable. That REVERSES an
    /// earlier narrowing of the rule to audience-side
    /// (<c>IsForVisitor</c>) types, under which a media, sponsor, staff or fellow
    /// exhibitor badge answered the same 404 as an unknown code. Media, sponsor
    /// and staff attendees are attendees; a booth that meets one captures the lead
    /// like any other.</para>
    ///
    /// <para><c>IsActive</c> is deliberately KEPT: a deactivated (soft-deleted)
    /// account is not a valid attendee, and the read path must never project a
    /// live contact card for one.</para>
    ///
    /// <para>Held as one expression so the capture path and the READ
    /// path apply exactly the same rule and can never drift.</para></summary>
    private static readonly Expression<Func<UserProfile, bool>> IsCapturableSubject =
        profile => profile.IsActive;

    public async Task<VisitorCard> ScanByBadgeAsync(
        Guid exhibitorUserId, string qrId, string? note,
        CancellationToken cancellationToken = default)
    {
        var exhibitor = await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        // Canonicalise first. An offline badge arrives as a
        // ~61-character encrypted blob, not a QrId, so the direct lookup below
        // would miss it and report an unknown badge. A minted serial passes
        // through unchanged.
        var normalised = qrResolver.ToStoredQrId(qrId ?? string.Empty);
        if (normalised.Length == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, 400,
                "A badge code is required.",
                "رمز البطاقة مطلوب.");
        }

        // The SUBJECT must be eligible too, not just the caller: any
        // ACTIVE badge holder (see IsCapturableSubject). A deactivated account
        // returns the same 404 as an unknown code — the caller never learns
        // whether the code exists.
        // Resolved to the subject's PROFILE id, which every attendee has. It used
        // to select their Identity account, so a walk-in or bulk-minted badge —
        // which has none — answered the same 404 as an unknown code, and no amount
        // of re-scanning would have captured them.
        var visitorId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(IsCapturableSubject)
            .Where(p => p.QrId == normalised)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (visitorId is null)
        {
            throw new ApiException(ErrorCodes.NotFound, 404,
                "No visitor badge matches this code.",
                "لا توجد بطاقة زائر مطابقة لهذا الرمز.");
        }
        if (visitorId.Value == exhibitor.OfficerProfileId)
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

        // Idempotent per (BOOTH, visitor). A repeat scan refreshes
        // the note, and a colleague re-scanning the same visitor updates the
        // booth's existing lead instead of forking a second private copy of it.
        // Legacy rows carry no ExhibitorId, so the caller's own un-backfilled
        // capture of the same visitor still counts as the existing one.
        //
        // The two kinds can be live AT ONCE: a colleague
        // captures the visitor for the booth while this officer still holds their
        // own un-backfilled legacy row of that visitor. Taking whichever row the
        // database happened to return first then either stamped the booth id onto
        // the LEGACY row — a second ACTIVE (booth, visitor) pair, which the
        // filtered unique index rejects, surfacing as a 500 — or picked the booth
        // row and left the legacy one active, so the booth listed the visitor
        // twice. The whole matched set is loaded and resolved deliberately
        // instead; the unordered "first" is never the deciding factor.
        var matches = await appDbContext.ExhibitorVisitorScans
            .Where(s => s.VisitorProfileId == visitorId.Value
                && s.IsActive
                && (s.ExhibitorId == exhibitor.Id
                    || (s.ExhibitorId == null && s.ExhibitorUserId == exhibitorUserId)))
            .ToListAsync(cancellationToken);

        // The BOOTH-scoped row wins. It is the row the booth's officers share and
        // the one the filtered unique index protects — and that index is what
        // makes the choice unambiguous: at most ONE active row can carry this
        // (ExhibitorId, VisitorProfileId), so there is never a second booth row to
        // arbitrate between. With no booth row the newest legacy row is adopted
        // into the booth instead (the hand-over), which is index-safe
        // precisely because no active booth row exists for this pair.
        // The legacy tie-break is the migration's, so a row collapsed by hand and
        // a row collapsed here are decided the same way: freshest write wins.
        var existing = matches.FirstOrDefault(s => s.ExhibitorId == exhibitor.Id)
            ?? matches
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .ThenByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();

        var now = timeProvider.SimfNow();
        if (existing is not null)
        {
            existing.Note = trimmed;
            existing.UpdatedAt = now;
            // Adopt a legacy row into the booth on the first re-scan, so the list
            // stops depending on the person who happened to capture it.
            existing.ExhibitorId ??= exhibitor.Id;

            // Converge the rows that did not win. They
            // are the caller's own legacy captures of a visitor the booth already
            // holds, so they can neither be adopted (the booth pair is taken) nor
            // be left active (the booth would list the visitor twice). Soft-delete
            // is the project's remove contract — the row and its consent trail
            // survive, it simply stops being a second live copy of one lead.
            foreach (var superseded in matches.Where(s => s.Id != existing.Id))
            {
                superseded.Deactivate();
                superseded.UpdatedAt = now;
                logger.LogInformation(
                    "Converged legacy exhibitor capture {CaptureId} of visitor {VisitorProfileId} "
                    + "into the booth's capture {WinningCaptureId} for exhibitor {ExhibitorId}.",
                    superseded.Id, visitorId.Value, existing.Id, exhibitor.Id);
            }
        }
        else
        {
            appDbContext.ExhibitorVisitorScans.Add(new ExhibitorVisitorScan
            {
                Id = Guid.NewGuid(),
                ExhibitorUserId = exhibitorUserId,
                ExhibitorId = exhibitor.Id,
                VisitorProfileId = visitorId.Value,
                Note = trimmed,
                IsActive = true,
                CreatedAt = now,
            });
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        if (existing is null)
        {
            await NotifyVisitorCapturedAsync(visitorId.Value, exhibitor, cancellationToken);
        }

        var cards = await ResolveCardsAsync(new[] { visitorId.Value }, cancellationToken);
        var card = cards[visitorId.Value];

        // Only a NEW capture mails the lead out; a repeat scan is a
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
        var exhibitor = await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        var rows = await appDbContext.ExhibitorVisitorScans
            .AsNoTracking()
            .Where(CapturedByBooth(exhibitorUserId, exhibitor.Id))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.VisitorProfileId, s.Note, s.CreatedAt })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Array.Empty<ExhibitorVisitorRow>();
        }

        // Re-run the capture-time SUBJECT test on the READ path.
        // Capture-time-only enforcement left every row taken while the old rule
        // was in force (no IsActive test at all) still projecting a full live card
        // — login email + both mobile numbers — for a since-deactivated subject. A
        // subject that is no longer capturable simply drops out of the list, so no
        // PII is projected for it (this also covers a subject whose profile row has
        // gone). The rule itself widened to "any active badge holder", so a
        // media / sponsor / staff capture now legitimately stays in the list.
        var subjectIds = rows.Select(r => r.VisitorProfileId).Distinct().ToList();
        var eligibleSubjectIds = (await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(IsCapturableSubject)
            .Where(p => subjectIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var visible = rows
            .Where(r => eligibleSubjectIds.Contains(r.VisitorProfileId))
            .ToList();
        if (visible.Count == 0)
        {
            return Array.Empty<ExhibitorVisitorRow>();
        }

        var cards = await ResolveCardsAsync(
            visible.Select(r => r.VisitorProfileId).Distinct().ToList(), cancellationToken);

        return visible
            .Select(r => new ExhibitorVisitorRow(
                r.Id, r.CreatedAt, r.Note, cards[r.VisitorProfileId]))
            .ToList();
    }

    public async Task RemoveCaptureAsync(
        Guid exhibitorUserId, Guid captureId, CancellationToken cancellationToken = default)
    {
        var exhibitor = await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        // Soft-delete, the project convention (BaseAuditEntity
        // .Deactivate), so the lead leaves the booth's list without destroying the
        // capture record. Idempotent: the row is already filtered to the ACTIVE
        // ones, so a repeat delete is a no-op 200 rather than a 404 — the same
        // contract My Contacts' remove honours.
        var capture = await appDbContext.ExhibitorVisitorScans
            .Where(CapturedByBooth(exhibitorUserId, exhibitor.Id))
            .FirstOrDefaultAsync(s => s.Id == captureId, cancellationToken);
        if (capture is null)
        {
            return;
        }
        capture.Deactivate();
        capture.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ExhibitorLeadRemoved,
            exhibitorUserId,
            $"captureId={captureId}; exhibitorId={exhibitor.Id}",
            cancellationToken);
    }

    public async Task<VisitorCard> GetCaptureCardAsync(
        Guid exhibitorUserId, Guid captureId, CancellationToken cancellationToken = default)
    {
        var exhibitor = await EnsureExhibitorAsync(exhibitorUserId, cancellationToken);

        var subjectId = await appDbContext.ExhibitorVisitorScans
            .AsNoTracking()
            .Where(CapturedByBooth(exhibitorUserId, exhibitor.Id))
            .Where(s => s.Id == captureId)
            .Select(s => (Guid?)s.VisitorProfileId)
            .FirstOrDefaultAsync(cancellationToken);
        if (subjectId is null)
        {
            throw CaptureNotFound();
        }

        // The export runs the SAME subject test as the read path:
        // a lead the list refuses to project must not be exportable through a
        // second door either.
        var eligible = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(IsCapturableSubject)
            .AnyAsync(p => p.Id == subjectId.Value, cancellationToken);
        if (!eligible)
        {
            throw CaptureNotFound();
        }

        var cards = await ResolveCardsAsync(new[] { subjectId.Value }, cancellationToken);
        return cards[subjectId.Value];
    }

    /// <summary>The booth's active captures: everything tagged to the
    /// exhibitor, plus the caller's own legacy rows that predate the column (the
    /// migration backfills from membership, so only a capturer who had no
    /// membership at migration time leaves one behind). Held as one expression so
    /// the list, the delete and the export can never disagree about what belongs
    /// to the booth.</summary>
    private static Expression<Func<ExhibitorVisitorScan, bool>> CapturedByBooth(
        Guid exhibitorUserId, Guid exhibitorId) =>
        scan => scan.IsActive
            && (scan.ExhibitorId == exhibitorId
                || (scan.ExhibitorId == null && scan.ExhibitorUserId == exhibitorUserId));

    private static ApiException CaptureNotFound() =>
        new(ErrorCodes.NotFound, 404,
            "That captured visitor is not on your booth's list.",
            "هذا الزائر غير موجود في قائمة جناحك.");

    /// <summary>Emails the captured lead to the exhibitor's own account
    /// address. Fire-and-forget by contract: the capture row is already committed,
    /// so an exhibitor with no account email is logged and skipped, and an enqueue
    /// failure is swallowed + audited by <see cref="EmailQueueExtensions.TryEnqueueAsync"/>
    /// — the 200 and the lead row stand either way. The national ID (encrypted at
    /// rest) and the raw badge QR id are deliberately NOT in the message.</summary>
    private async Task EmailLeadToExhibitorAsync(
        Guid exhibitorUserId, VisitorCard card, string? note,
        DateTime scannedAt, CancellationToken cancellationToken)
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
            // Saudi wall clock, 12-hour. No user-facing zoned stamp.
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

    /// <summary>403 unless the caller is a genuine EXHIBITOR: an
    /// active profile whose assigned profile type carries
    /// <see cref="MobileAppRole.Exhibitor"/>. The former test admitted any
    /// profile type that merely was NOT a visitor type, so Staff / Moderator /
    /// Media / Sponsor tokens could call the scan + list endpoints and harvest
    /// visitor PII (login email and both mobile numbers).
    ///
    /// <para>The role alone is not authority. It is granted at
    /// provisioning time and then lives on the PERSON's profile, so it outlived
    /// their booth: dropping an officer from an exhibitor left them able to go on
    /// scanning badges and reading contact cards. A CURRENT membership of a live
    /// exhibitor is required alongside the role, so revoking the membership (or
    /// closing the exhibitor) revokes the tools with it.</para>
    ///
    /// <para>No cross-database work: <c>ProfileType</c>,
    /// <c>ExhibitorMembership</c> and <c>Exhibitor</c> all live on the App DB
    /// beside <c>UserProfile</c>, and <c>MobileAppRole</c> is the same column the
    /// JWT's app role is resolved from
    /// (<c>UserProfileRepository.GetAssignedProfileTypeRoleAsync</c>).</para>
    ///
    /// <para>Returns the booth's display names for the subject
    /// notification.</para></summary>
    private async Task<ExhibitorIdentity> EnsureExhibitorAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var officer = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId
                && p.IsActive
                && p.ProfileType != null
                && p.ProfileType.MobileAppRole == MobileAppRole.Exhibitor)
            .Select(p => new { p.Id, p.Name, p.NameArabic })
            .FirstOrDefaultAsync(cancellationToken);
        if (officer is null)
        {
            throw NotAnActiveBoothOfficer();
        }

        // The membership half. Second query rather than one clever
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
            .Select(m => new { m.ExhibitorId, m.Exhibitor!.Name, m.Exhibitor!.NameArabic })
            .FirstOrDefaultAsync(cancellationToken);
        if (booth is null)
        {
            throw NotAnActiveBoothOfficer();
        }

        // Name the EXHIBITOR, not the account. A CP-provisioned
        // officer gets a stub UserProfile with no Name / NameArabic at all
        // (AdminAccountService.CreateAccountAsync), so reading the name off the
        // scanning account degraded the capture notice to "An exhibitor" for
        // exactly the accounts the CP creates. The officer's own name is kept as
        // the fallback for a self-registered exhibitor whose booth row somehow
        // carries a blank name.
        return new ExhibitorIdentity(
            booth.ExhibitorId,
            string.IsNullOrWhiteSpace(booth.Name) ? officer.Name : booth.Name,
            string.IsNullOrWhiteSpace(booth.NameArabic)
                ? officer.NameArabic
                : booth.NameArabic,
            officer.Id);
    }

    /// <summary>One 403 for both halves of the exhibitor test (the role and a
    /// current membership) — the caller learns they may not use the
    /// booth tools, not which half of the rule they failed.</summary>
    private static ApiException NotAnActiveBoothOfficer() =>
        new(ErrorCodes.Forbidden, 403,
            "Only exhibitor accounts with a current booth membership can scan visitor badges.",
            "مسح بطاقات الزوار متاح فقط لحسابات العارضين المرتبطة بجناح فعّال.");

    /// <summary>Tell the visitor, in-app, that their
    /// contact card was shared with the NAMED exhibitor whose officer scanned their
    /// entry badge (<see cref="EnsureExhibitorAsync"/> resolves the name). Raised
    /// once per new capture only; the idempotent re-scan path (which merely
    /// refreshes the note) stays silent. Best-effort like the other request /
    /// booking flows — a dispatch failure never undoes the committed capture.
    /// Notifications live on the Identity DB, dispatched through its own unit of
    /// work, so this is not a cross-database transaction.
    ///
    /// <para>A notification is delivered to an ACCOUNT, so a lead who holds none —
    /// a walk-in or a bulk-minted badge — cannot be told. Their capture still
    /// stands; there is simply no device or mailbox to reach.</para></summary>
    private async Task NotifyVisitorCapturedAsync(
        Guid visitorProfileId, ExhibitorIdentity exhibitor,
        CancellationToken cancellationToken)
    {
        var visitorUserId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.Id == visitorProfileId)
            .Select(p => p.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (visitorUserId is not { } recipientId)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(exhibitor.Name)
            ? "An exhibitor"
            : exhibitor.Name.Trim();
        var nameArabic = string.IsNullOrWhiteSpace(exhibitor.NameArabic)
            ? "أحد العارضين"
            : exhibitor.NameArabic.Trim();
        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = recipientId,
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

    /// <summary>The EXHIBITOR the scanning officer represents — its id (the booth
    /// every capture is tagged to and every list is scoped by) and its
    /// bilingual display name, carried from the authorisation query into the
    /// subject notification so the visitor is told WHO their card went to.
    /// <para><see cref="OfficerProfileId"/> is the SCANNING officer's own attendee
    /// record, carried so the self-scan check compares like with like: captures are
    /// keyed by profile, so comparing the subject against the officer's account id
    /// would never match and would let an officer capture themselves.</para></summary>
    private sealed record ExhibitorIdentity(
        Guid Id, string Name, string NameArabic, Guid OfficerProfileId);

    /// <summary>Batch-resolves visitor cards from profiles + org / country lookups
    /// (App DB) and a single email round-trip (Identity DB). A subject with no
    /// profile resolves to an unavailable card. Mirrors the contact-share card
    /// projection — kept local to avoid coupling the two services.
    ///
    /// <para>Keyed by PROFILE id, which is what a capture row carries. The email is
    /// the one field that still needs an account, so a lead with none simply has no
    /// email on their card; every other field resolves as usual rather than the
    /// whole lead dropping out of the list.</para></summary>
    private async Task<Dictionary<Guid, VisitorCard>> ResolveCardsAsync(
        IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, VisitorCard>();
        if (profileIds.Count == 0)
        {
            return result;
        }

        var profiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
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

        // Only the subjects that HAVE an account can have an email, so ask about
        // those alone; a walk-in contributes no id to this round-trip.
        var accountIds = profiles
            .Where(p => p.UserId is not null)
            .Select(p => p.UserId!.Value)
            .Distinct()
            .ToList();
        var emails = accountIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await userDirectory.GetEmailsAsync(accountIds, cancellationToken);

        foreach (var profileId in profileIds.Distinct())
        {
            var profile = profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile is null)
            {
                result[profileId] = new VisitorCard(
                    Guid.Empty, string.Empty, string.Empty, null, null, null,
                    null, null, null, null, null, null, null, Available: false,
                    UserProfileId: profileId);
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

            string? email = null;
            if (profile.UserId is { } accountId)
            {
                emails.TryGetValue(accountId, out email);
            }

            result[profileId] = new VisitorCard(
                // The SHIPPED UserId field. Empty when the lead holds no account —
                // a walk-in or a bulk-minted badge — which is exactly why the
                // appended UserProfileId below is the one to identify them by.
                profile.UserId ?? Guid.Empty,
                profile.Name, profile.NameArabic, profile.JobTitle,
                profile.JobTitleArabic,
                orgEn, orgAr, email, profile.SaudiMobile, profile.InternationalMobile,
                countryId, countryEn, countryAr, Available: true,
                UserProfileId: profileId);
        }

        return result;
    }
}
