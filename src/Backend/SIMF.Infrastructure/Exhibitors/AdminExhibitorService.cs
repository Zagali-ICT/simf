// Tests: SIMF.Api.Tests/ExhibitorsTests.cs, SIMF.Api.Tests/ExhibitorVisitorScanTests.cs,
// SIMF.Api.Tests/ExhibitorAccountRevokeTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibitors;

/// <summary>Admin CRUD over exhibitors plus account provisioning.
/// Mirrors AdminDelegationService for the CRUD; account provisioning reuses the
/// existing admin provisioning pipeline
/// (<see cref="IAdminUserProvisioningService.CreateOtherAsync"/>) so we never
/// hand-roll UserManager — the provisioned account is a partner-side booth
/// officer carrying the exhibitor profile type (DEF-EXH-005), tagged to the
/// exhibitor via an ExhibitorMembership row.
/// <para><see cref="LinkAccountAsync"/> attaches an EXISTING account to an
/// exhibitor. Provisioning used to be the only writer of ExhibitorMembership, so
/// an exhibitor-typed account created through the generic Others pipeline had no
/// membership and was locked out of the booth tools (DEF-EXH-006) with no CP path
/// to attach it.</para>
/// <para><see cref="RevokeAccountAsync"/> is the counterpart, and closes the other
/// half of the same gap: both writers created memberships and nothing ever cleared
/// one, so an account kept badge scanning and the booth's visitor contact cards
/// until the entire exhibitor was retired.</para></summary>
internal sealed class AdminExhibitorService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAdminUserProvisioningService provisioning,
    IAssetService assetService,
    SimfIdentityDbContext identityDbContext) : IAdminExhibitorService
{
    /// <summary>The audit event a revoked booth membership is recorded under.
    ///
    /// <para>Module-local rather than a field on the shared <c>AuditEvents</c>
    /// class, which the News events were also promoted out of. The string value is
    /// the audit contract, so it follows the module's existing <c>Exhibitor.*</c>
    /// shape: promoting it to the shared class is then a move, and never a rename
    /// that would orphan the rows already written under it.</para></summary>
    private const string ExhibitorAccountRevokedEvent = "Exhibitor.AccountRevoked";

    /// <summary>
    /// The grid contract for /admin/exhibitors: one entry per key
    /// ExhibitorsList.razor can send, as both its filter and its sort. A key not
    /// declared here is a 400, not a silently ignored request. AccountCount is
    /// absent on purpose — it is a computed sub-query the grid neither sorts nor
    /// filters on.
    /// </summary>
    private static readonly GridColumns<Exhibitor> Columns = new GridColumns<Exhibitor>()
        .Add("nameEn", exhibitor => exhibitor.Name, searchable: true)
        .Add("nameAr", exhibitor => exhibitor.NameArabic, searchable: true)
        .Add("isActive", exhibitor => exhibitor.IsActive)
        .DefaultOrder("nameAr")
        .PageSize(fallback: 25, max: 200);

    /// <summary>Not static, unlike every other grid projection: AccountCount is a
    /// correlated sub-query over a table Exhibitor has no navigation to, so the
    /// projection has to close over the context. HasExhibitorLogo is deliberately
    /// left at its default and filled in once the page has materialised — it comes
    /// from the file store, not from this query.</summary>
    private Expression<Func<Exhibitor, AdminExhibitorSummary>> ToSummary =>
        exhibitor => new AdminExhibitorSummary(
            exhibitor.Id, exhibitor.Name, exhibitor.NameArabic,
            exhibitor.ContactEmail, exhibitor.ContactPhone, exhibitor.Website,
            appDbContext.Set<ExhibitorMembership>().Count(
                membership => membership.ExhibitorId == exhibitor.Id && membership.IsActive),
            exhibitor.IsActive, exhibitor.CreatedAt, exhibitor.Tier);

    public async Task<GridPage<AdminExhibitorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var page = await appDbContext.Exhibitors.ToGridPageAsync(
            query, Columns, exhibitor => exhibitor.Id, ToSummary, cancellationToken);

        // The exhibitor owns its own ExhibitorLogo (the app + the grid render this,
        // not the linked Contact's) — one batched query over the page's exhibitor
        // ids rather than a per-row read.
        var exhibitorLogoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.ExhibitorLogo,
            page.Items.Select(row => row.Id).ToList(),
            cancellationToken);

        return GridPage<AdminExhibitorSummary>.Of(
            page.Items
                .Select(row => row with { HasExhibitorLogo = exhibitorLogoOwners.Contains(row.Id) })
                .ToList(),
            page.Total, page.Skip, page.Top);
    }

    public async Task<AdminExhibitorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.Exhibitors.AsNoTracking()
            .Where(exhibitor => exhibitor.Id == id)
            .Select(exhibitor => new AdminExhibitorDetail(
                exhibitor.Id, exhibitor.Name, exhibitor.NameArabic,
                exhibitor.ContactEmail, exhibitor.ContactPhone, exhibitor.Website,
                exhibitor.IsActive, exhibitor.CreatedAt, exhibitor.UpdatedAt, exhibitor.Tier,
                exhibitor.CountryId,
                exhibitor.Country != null ? exhibitor.Country.Name : null,
                exhibitor.Country != null ? exhibitor.Country.NameArabic : null,
                exhibitor.PhoneSecondary, exhibitor.FacebookUrl, exhibitor.XUrl,
                exhibitor.LinkedInUrl, exhibitor.InstagramUrl,
                exhibitor.City, exhibitor.CityArabic,
                exhibitor.Latitude, exhibitor.Longitude))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminExhibitorDetail> CreateAsync(
        Guid actorUserId, CreateExhibitorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website, request.Tier);
        ValidateContactFields(
            request.PhoneSecondary, request.FacebookUrl, request.XUrl,
            request.LinkedInUrl, request.InstagramUrl, request.City,
            request.CityArabic, request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);
        var now = timeProvider.SimfNow();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = request.NameEn.Trim(),
            NameArabic = request.NameAr.Trim(),
            ContactEmail = NormaliseOptional(request.ContactEmail),
            ContactPhone = NormaliseOptional(request.ContactPhone),
            Website = NormaliseOptional(request.Website),
            Tier = request.Tier,
            CountryId = request.CountryId,
            PhoneSecondary = NormaliseOptional(request.PhoneSecondary),
            FacebookUrl = NormaliseOptional(request.FacebookUrl),
            XUrl = NormaliseOptional(request.XUrl),
            LinkedInUrl = NormaliseOptional(request.LinkedInUrl),
            InstagramUrl = NormaliseOptional(request.InstagramUrl),
            City = NormaliseOptional(request.City),
            CityArabic = NormaliseOptional(request.CityArabic),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Exhibitors.Add(exhibitor);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ExhibitorCreated,
            actorUserId,
            $"exhibitorId={exhibitor.Id}; name={exhibitor.NameArabic}",
            cancellationToken);

        return (await GetAsync(exhibitor.Id, cancellationToken))!;
    }

    public async Task<AdminExhibitorDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateExhibitorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website, request.Tier);
        ValidateContactFields(
            request.PhoneSecondary, request.FacebookUrl, request.XUrl,
            request.LinkedInUrl, request.InstagramUrl, request.City,
            request.CityArabic, request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);
        var exhibitor = await appDbContext.Exhibitors
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");

        exhibitor.Name = request.NameEn.Trim();
        exhibitor.NameArabic = request.NameAr.Trim();
        exhibitor.ContactEmail = NormaliseOptional(request.ContactEmail);
        exhibitor.ContactPhone = NormaliseOptional(request.ContactPhone);
        exhibitor.Website = NormaliseOptional(request.Website);
        exhibitor.Tier = request.Tier;
        exhibitor.CountryId = request.CountryId;
        exhibitor.PhoneSecondary = NormaliseOptional(request.PhoneSecondary);
        exhibitor.FacebookUrl = NormaliseOptional(request.FacebookUrl);
        exhibitor.XUrl = NormaliseOptional(request.XUrl);
        exhibitor.LinkedInUrl = NormaliseOptional(request.LinkedInUrl);
        exhibitor.InstagramUrl = NormaliseOptional(request.InstagramUrl);
        exhibitor.City = NormaliseOptional(request.City);
        exhibitor.CityArabic = NormaliseOptional(request.CityArabic);
        exhibitor.Latitude = request.Latitude;
        exhibitor.Longitude = request.Longitude;
        // The edit form's toggle moves the row both ways, so the soft-delete stamp has
        // to move with it: DeactivateAsync now records WHEN a row went away, and a
        // restore that left the old stamp behind would read as deleted-yet-active.
        if (request.IsActive)
        {
            exhibitor.IsActive = true;
            exhibitor.DeletedAt = null;
        }
        else
        {
            exhibitor.Deactivate();
        }

        exhibitor.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ExhibitorUpdated,
            actorUserId,
            $"exhibitorId={exhibitor.Id}; active={exhibitor.IsActive}",
            cancellationToken);

        return (await GetAsync(exhibitor.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var exhibitor = await appDbContext.Exhibitors
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        if (!exhibitor.IsActive) { return; }

        await EnsureNoBoothIsMarkedOnVenueMapAsync(id, cancellationToken);

        exhibitor.Deactivate();
        exhibitor.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ExhibitorDeactivated,
            actorUserId,
            $"exhibitorId={exhibitor.Id}",
            cancellationToken);
    }

    /// <summary>Refuses to retire an exhibitor while one of its booths is still
    /// pinned on the venue map.
    ///
    /// <para>The exhibitor's own IsActive is not what the map reads, which is why
    /// this looks a level deeper than AdminBoothService's own guard. The public
    /// booth projection drops a booth whose linked Exhibitor is inactive even
    /// though the booth row itself stays active, so soft-deleting the exhibitor
    /// leaves every map node pointing at a booth the public endpoint no longer
    /// serves. Deactivating the booth directly is already refused with
    /// BOOTH_IN_USE; without this, the same orphan was reachable by retiring the
    /// company standing in it.</para>
    ///
    /// <para>Only ACTIVE booths count: an already-retired booth cannot have a
    /// live node marking it, because the booth guard blocked that on the way
    /// out. The blocking booth's floor code is named in the message so the admin
    /// knows which node to remove, and the code is the same token in both
    /// languages.</para></summary>
    private async Task EnsureNoBoothIsMarkedOnVenueMapAsync(
        Guid exhibitorId, CancellationToken cancellationToken)
    {
        var blockingBoothCode = await appDbContext.VenueMapNodes
            .AsNoTracking()
            .Where(node => node.IsActive
                && node.Booth != null
                && node.Booth.IsActive
                && node.Booth.ExhibitorId == exhibitorId)
            .Select(node => node.Booth!.Code)
            // Ordered so an exhibitor blocked by several booths reports the same
            // one every time; an error the admin cannot reproduce is a worse bug
            // than the sort is a cost, and the row count here is a handful.
            .OrderBy(code => code)
            .FirstOrDefaultAsync(cancellationToken);
        if (blockingBoothCode is null) { return; }

        throw new ApiException(
            ErrorCodes.ExhibitorInUse, 409,
            $"Booth '{blockingBoothCode}' belongs to this exhibitor and is still marked on the venue map, so the exhibitor cannot be deactivated. Remove that venue-map node first.",
            $"الجناح '{blockingBoothCode}' تابع لهذا العارض وما زال محدداً على خريطة المكان، لذا لا يمكن إلغاء تفعيل العارض. احذف عقدة الخريطة المرتبطة به أولاً.");
    }

    public async Task<IReadOnlyList<ExhibitorAccountSummary>> ListAccountsAsync(
        Guid exhibitorId, CancellationToken cancellationToken = default)
    {
        // Confirm the exhibitor exists so a stranger id 404s instead of
        // silently returning an empty list.
        var exists = await appDbContext.Exhibitors
            .AsNoTracking()
            .AnyAsync(exhibitor => exhibitor.Id == exhibitorId, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        }

        var memberships = await appDbContext.Set<ExhibitorMembership>()
            .AsNoTracking()
            .Where(membership => membership.ExhibitorId == exhibitorId && membership.IsActive)
            .OrderBy(membership => membership.CreatedAt)
            .Select(membership => new
            {
                membership.Id,
                membership.UserId,
                membership.ContactName,
                membership.RoleLabel,
                membership.IsActive,
                membership.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            return Array.Empty<ExhibitorAccountSummary>();
        }

        // Resolve the account emails and display names cross-context (UserId is a
        // logical FK to SimfUser on the Identity DB — no DB-level JOIN is possible,
        // so read the small id set back AsNoTracking). The display name comes back
        // in the same round trip because the membership stores only an override.
        var userIds = memberships.Select(membership => membership.UserId).ToList();
        var accounts = await identityDbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .ToListAsync(cancellationToken);
        var emailsById = accounts.ToDictionary(
            account => account.Id, account => account.Email ?? string.Empty);
        var displayNamesById = accounts.ToDictionary(
            account => account.Id, account => account.DisplayName);

        return memberships
            .Select(membership => new ExhibitorAccountSummary(
                membership.Id,
                membership.UserId,
                ResolveContactName(
                    membership.ContactName,
                    displayNamesById.GetValueOrDefault(membership.UserId) ?? string.Empty,
                    emailsById.GetValueOrDefault(membership.UserId) ?? string.Empty),
                emailsById.GetValueOrDefault(membership.UserId) ?? string.Empty,
                membership.RoleLabel,
                membership.IsActive,
                membership.CreatedAt))
            .ToList();
    }

    public async Task<ExhibitorAccountSummary> ProvisionAccountAsync(
        Guid actorUserId, Guid exhibitorId, ProvisionExhibitorAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var exhibitor = await LoadActiveExhibitorAsync(exhibitorId, cancellationToken);

        var contactName = (request.ContactName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        if (contactName.Length is 0 or > 256 || email.Length is 0 or > 320)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 400,
                "Contact name (1-256) and email (1-320) are required.",
                "اسم جهة الاتصال (1-256) والبريد الإلكتروني (1-320) مطلوبان.");
        }
        var roleLabel = NormaliseOptional(request.RoleLabel);
        if (roleLabel is { Length: > 128 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 400,
                "Role label must be 128 characters or fewer.",
                "يجب ألا يتجاوز المسمى الوظيفي 128 حرفاً.");
        }

        // Reuse the existing admin provisioning pipeline (no RBAC role). It
        // validates the email-already-registered case and throws ApiException on
        // conflict. DEF-EXH-005: the account is provisioned with the EXHIBITOR
        // profile type, not with no profile type at all — the lead-capture
        // endpoints authorise on ProfileType.MobileAppRole == Exhibitor,
        // so a type-less account could never scan the booth's own visitors. The
        // exhibitor type is partner-side (IsForVisitor=false), which only the
        // Other pipeline accepts; CreateVisitorAsync enforces audience scope.
        var created = await provisioning.CreateOtherAsync(
            actorUserId,
            new AdminCreateOtherRequest
            {
                Email = email,
                DisplayName = contactName,
                ProfileTypeId = await ResolveExhibitorProfileTypeIdAsync(cancellationToken),
            },
            cancellationToken);

        var now = timeProvider.SimfNow();
        var membership = new ExhibitorMembership
        {
            Id = Guid.NewGuid(),
            ExhibitorId = exhibitor.Id,
            UserId = created.UserId,
            // The name was just written to the account as its DisplayName, which is
            // where it belongs; repeating it here would be a second copy of one fact
            // across the two databases, and it would go stale the moment the account
            // is renamed. Blank means "no override" and reads back off the account.
            ContactName = string.Empty,
            RoleLabel = roleLabel,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Set<ExhibitorMembership>().Add(membership);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ExhibitorAccountProvisioned,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = created.UserId,
            SubjectEmail = created.Email,
            Detail = $"exhibitorId={exhibitor.Id}; membershipId={membership.Id}",
        }, cancellationToken);

        return new ExhibitorAccountSummary(
            membership.Id,
            membership.UserId,
            contactName,
            created.Email,
            membership.RoleLabel,
            membership.IsActive,
            membership.CreatedAt);
    }

    /// <summary>Attach an EXISTING account to this exhibitor by writing the
    /// <see cref="ExhibitorMembership"/> that <see cref="ProvisionAccountAsync"/>
    /// would otherwise be the only source of.
    ///
    /// <para>Why this exists: DEF-EXH-006 made a CURRENT membership half the
    /// lead-capture authorisation, and provisioning is the only writer of that row.
    /// An exhibitor-typed account created through the generic Others pipeline
    /// (<c>POST /admin/others</c>) or the Others walk-in desk therefore came out
    /// with the right profile type and NO membership — 403 on badge scan and on My
    /// Visitors, with nothing in the Control Panel able to attach it to a booth.
    /// The scanner-side controls themselves are unchanged (the owner decision
    /// widened the SUBJECT rule only): the caller must still be exhibitor-typed AND
    /// hold a live membership, so revoking a membership still revokes the tools.</para>
    ///
    /// <para>The account must already carry an active exhibitor-mapped profile type
    /// — linking deliberately does not mutate the account's profile type, because
    /// that silently changes the app role a different admin assigned. An admin sets
    /// the type on the Others page and then links here.</para>
    ///
    /// <para>The account lives on the Identity DB and the membership on the
    /// App DB; they are resolved with two separate queries on two contexts, never a
    /// cross-database join, and only the App-DB row is written.</para></summary>
    public async Task<ExhibitorAccountSummary> LinkAccountAsync(
        Guid actorUserId, Guid exhibitorId, LinkExhibitorAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var exhibitor = await LoadActiveExhibitorAsync(exhibitorId, cancellationToken);

        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length is 0 or > 320)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 400,
                "The account email is required (1-320 characters).",
                "البريد الإلكتروني للحساب مطلوب (من 1 إلى 320 حرفاً).");
        }
        var contactNameOverride = NormaliseOptional(request.ContactName);
        if (contactNameOverride is { Length: > 256 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 400,
                "Contact name must be 256 characters or fewer.",
                "يجب ألا يتجاوز اسم جهة الاتصال 256 حرفاً.");
        }
        var roleLabel = NormaliseOptional(request.RoleLabel);
        if (roleLabel is { Length: > 128 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 400,
                "Role label must be 128 characters or fewer.",
                "يجب ألا يتجاوز المسمى الوظيفي 128 حرفاً.");
        }

        // Identity DB (read-only). NormalizedEmail is what Identity itself matches
        // on, so the lookup is case-insensitive without a collation assumption.
        var normalisedEmail = email.ToUpperInvariant();
        var account = await identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalisedEmail)
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountNotFound, 404,
                "No account is registered under this email.",
                "لا يوجد حساب مسجّل بهذا البريد الإلكتروني.");
        }

        // App DB — the same column the lead-capture endpoints authorise on
        // (ProfileType.MobileAppRole). Linking an account that does not
        // carry it would produce a membership that still cannot scan.
        var isExhibitorTyped = await appDbContext.UserProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.UserId == account.Id
                    && profile.IsActive
                    && profile.ProfileType != null
                    && profile.ProfileType.MobileAppRole == MobileAppRole.Exhibitor,
                cancellationToken);
        if (!isExhibitorTyped)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountNotEligible, 409,
                "This account does not carry an exhibitor profile type. Assign it an exhibitor profile type before linking it to a booth.",
                "هذا الحساب لا يحمل نوع ملف شخصي للعارضين. يرجى تعيين نوع ملف «عارض» للحساب قبل ربطه بجناح.");
        }

        // At most one ACTIVE membership per account (the filtered unique index on
        // ExhibitorMembership.UserId). Answer 409 rather than letting the insert
        // fail with a raw database error.
        var alreadyLinked = await appDbContext.Set<ExhibitorMembership>()
            .AsNoTracking()
            .AnyAsync(
                membership => membership.UserId == account.Id && membership.IsActive,
                cancellationToken);
        if (alreadyLinked)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountAlreadyLinked, 409,
                "This account already belongs to an exhibitor. Remove it from that exhibitor before linking it here.",
                "هذا الحساب مرتبط بالفعل بعارض آخر. يرجى إلغاء ارتباطه قبل ربطه هنا.");
        }

        var accountEmail = account.Email ?? email;
        var contactName = ResolveContactName(
            contactNameOverride ?? string.Empty, account.DisplayName, accountEmail);

        var link = new ExhibitorMembership
        {
            Id = Guid.NewGuid(),
            ExhibitorId = exhibitor.Id,
            UserId = account.Id,
            // Only what the admin actually overrode is persisted. Defaulting a blank
            // field to the account's display name would copy it into this database,
            // where nothing would keep it in step with the account it came from.
            ContactName = contactNameOverride ?? string.Empty,
            RoleLabel = roleLabel,
            IsActive = true,
            CreatedAt = timeProvider.SimfNow(),
        };
        appDbContext.Set<ExhibitorMembership>().Add(link);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ExhibitorAccountLinked,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = account.Id,
            SubjectEmail = accountEmail,
            Detail = $"exhibitorId={exhibitor.Id}; membershipId={link.Id}",
        }, cancellationToken);

        return new ExhibitorAccountSummary(
            link.Id,
            link.UserId,
            contactName,
            accountEmail,
            link.RoleLabel,
            link.IsActive,
            link.CreatedAt);
    }

    /// <summary>Withdraw one account's booth access by soft-deleting its
    /// <see cref="ExhibitorMembership"/>.
    ///
    /// <para>Why this exists: <see cref="ProvisionAccountAsync"/> and
    /// <see cref="LinkAccountAsync"/> write that row and nothing anywhere cleared
    /// it, so once an account was attached it held the booth tools until the whole
    /// exhibitor was retired. Three readers lose the account the moment this runs —
    /// the lead-capture badge scan and the booth's visitor contact cards, the
    /// business-meeting notifications that fan out to every active membership, and
    /// the account count on the admin grid. An officer who leaves the company has
    /// to be removable on their own, without closing the booth.</para>
    ///
    /// <para>Soft revoke, never a hard delete. The row is the attribution trail for
    /// the visitor cards that account already captured, and each of those captures
    /// notified the visitor that their details had been shared; deleting the
    /// membership would leave that consent trail pointing at nothing.</para>
    ///
    /// <para>The membership is matched on its own id AND the exhibitor from the
    /// route, so an id belonging to a different booth answers 404 instead of
    /// letting one exhibitor's administrator revoke another's officer. Deliberately
    /// NOT routed through <see cref="LoadActiveExhibitorAsync"/>: that refuses an
    /// inactive exhibitor, which is right when adding an officer and backwards when
    /// removing one. Withdrawing access has to stay possible after a booth
    /// closes.</para></summary>
    public async Task RevokeAccountAsync(
        Guid actorUserId, Guid exhibitorId, Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        var membership = await appDbContext.Set<ExhibitorMembership>()
            .SingleOrDefaultAsync(
                row => row.Id == membershipId && row.ExhibitorId == exhibitorId,
                cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorAccountNotFound, 404,
                "No account with that membership id is attached to this exhibitor.",
                "لا يوجد حساب مرتبط بهذا العارض بهذا المعرّف.");

        // Answered rather than treated as a no-op: an admin pressing Revoke on a
        // membership somebody else already revoked has been told the access is
        // gone, which a silent 200 would leave them guessing about.
        if (!membership.IsActive)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorAccountInvalid, 409,
                "This account's exhibitor membership has already been revoked.",
                "تم إلغاء ارتباط هذا الحساب بهذا العارض بالفعل.");
        }

        membership.Deactivate();
        membership.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        // The account lives on the Identity database, so its address is a second
        // read on the other context rather than a join. The audit row keeps it
        // because the membership only ever held a bare Guid: a reader asking months
        // later who lost booth access cannot resolve that id from this database.
        var subjectEmail = await identityDbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == membership.UserId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = ExhibitorAccountRevokedEvent,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = membership.UserId,
            SubjectEmail = subjectEmail,
            Detail = $"exhibitorId={exhibitorId}; membershipId={membership.Id}",
        }, cancellationToken);
    }

    /// <summary>The contact name to show for a membership: the per-booth override
    /// when the admin set one, else the account's own display name, else its login
    /// email. Resolved on every read rather than frozen into the membership row, so
    /// renaming an account updates the booth's officer list with it.
    ///
    /// <para>Capped at the membership column's 256 characters so an overridden name
    /// stays comparable with what could have been stored there (an email may run to
    /// 320).</para></summary>
    private static string ResolveContactName(string overrideName, string displayName, string email)
    {
        if (!string.IsNullOrWhiteSpace(overrideName))
        {
            return overrideName.Trim();
        }

        var value = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim();
        return value.Length <= 256 ? value : value[..256];
    }

    /// <summary>The exhibitor an account operation runs against: it must exist and
    /// be active (a closed booth takes on no new officers). Shared by the provision
    /// and link paths so the two answer the same 404 / 409.</summary>
    private async Task<Exhibitor> LoadActiveExhibitorAsync(
        Guid exhibitorId, CancellationToken cancellationToken)
    {
        var exhibitor = await appDbContext.Exhibitors
            .SingleOrDefaultAsync(row => row.Id == exhibitorId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        if (!exhibitor.IsActive)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInactive, 409,
                "The exhibitor is not active; reactivate it before adding accounts.",
                "العارض غير نشط؛ يرجى إعادة تفعيله قبل إضافة الحسابات.");
        }
        return exhibitor;
    }

    /// <summary>DEF-EXH-005 — the ProfileTypes row a booth officer is provisioned
    /// under. Resolved by its <see cref="MobileAppRole"/> rather than by a name
    /// literal: the row is admin-curated at runtime (renameable, and an admin may
    /// add further exhibitor-mapped types), and MobileAppRole is exactly what the
    /// lead-capture endpoints authorise on. Partner-side by definition
    /// (<c>IsForVisitor=false</c>) so the Other provisioning pipeline accepts it.
    /// Deterministic pick — oldest first — when an admin has created more than one.
    /// The seeder creates the canonical row on every boot, so the 409 only fires
    /// when an admin has deactivated or re-mapped every exhibitor type.</summary>
    private async Task<Guid> ResolveExhibitorProfileTypeIdAsync(
        CancellationToken cancellationToken)
    {
        var profileTypeId = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(profileType => profileType.IsActive
                && !profileType.IsForVisitor
                && profileType.MobileAppRole == MobileAppRole.Exhibitor)
            .OrderBy(profileType => profileType.CreatedAt)
            .ThenBy(profileType => profileType.Name)
            .Select(profileType => (Guid?)profileType.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (profileTypeId is null)
        {
            throw new ApiException(
                ErrorCodes.AdminProfileTypeInvalid, 409,
                "No active exhibitor profile type exists. Create a partner profile type whose mobile app role is Exhibitor before provisioning booth accounts.",
                "لا يوجد نوع ملف شخصي فعّال للعارضين. يرجى إنشاء نوع ملف من نطاق الشركاء دوره في التطبيق «عارض» قبل إضافة حسابات الجناح.");
        }
        return profileTypeId.Value;
    }

    private static void Validate(
        string nameEn, string nameAr, string? contactEmail,
        string? contactPhone, string? website, ExhibitorTier? tier)
    {
        if (tier.HasValue && !Enum.IsDefined(typeof(ExhibitorTier), tier.Value))
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                "Exhibitor tier is not a recognised value.",
                "فئة العارض ليست قيمة معروفة.");
        }
        if (string.IsNullOrWhiteSpace(nameEn) || nameEn.Length > 256
            || string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                "Exhibitor name (EN + AR) must be between 1 and 256 characters.",
                "يجب أن يتراوح طول اسم العارض (إنجليزي + عربي) بين 1 و 256 حرفاً.");
        }
        if (contactEmail is { Length: > 320 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                "Contact email must be 320 characters or fewer.",
                "يجب ألا يتجاوز البريد الإلكتروني 320 حرفاً.");
        }
        if (contactPhone is { Length: > 32 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                "Contact phone must be 32 characters or fewer.",
                "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
        }
        if (website is { Length: > 512 })
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                "Website must be 512 characters or fewer.",
                "يجب ألا يتجاوز الموقع الإلكتروني 512 حرفاً.");
        }
    }

    // Validates the identity-card fields inlined from the removed shared
    // Contact directory. The email + primary phone are covered by Validate (they
    // reuse ContactEmail / ContactPhone); this covers the new inline set. Lengths
    // mirror the EF configuration; latitude and longitude are an all-or-nothing
    // pair with real-world ranges.
    private static void ValidateContactFields(
        string? phoneSecondary, string? facebook, string? xUrl,
        string? linkedIn, string? instagram, string? city, string? cityArabic,
        double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(phoneSecondary) && phoneSecondary.Length > 32)
        {
            throw Invalid("Phone numbers must be 32 characters or less.",
                "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
        }
        foreach (var url in new[] { facebook, xUrl, linkedIn, instagram })
        {
            if (!string.IsNullOrWhiteSpace(url) && url.Length > 256)
            {
                throw Invalid("Social URLs must be 256 characters or less.",
                    "يجب ألا يتجاوز رابط الشبكات الاجتماعية 256 حرفاً.");
            }
        }
        foreach (var cityValue in new[] { city, cityArabic })
        {
            if (!string.IsNullOrWhiteSpace(cityValue) && cityValue.Length > 128)
            {
                throw Invalid("City must be 128 characters or less.",
                    "يجب ألا تتجاوز المدينة 128 حرفاً.");
            }
        }
        if (latitude is null != (longitude is null))
        {
            throw Invalid("Latitude and longitude must be provided together.",
                "يجب إدخال خط العرض وخط الطول معاً.");
        }
        if (latitude is < -90 or > 90)
        {
            throw Invalid("Latitude must be between -90 and 90.",
                "يجب أن يكون خط العرض بين -90 و 90.");
        }
        if (longitude is < -180 or > 180)
        {
            throw Invalid("Longitude must be between -180 and 180.",
                "يجب أن يكون خط الطول بين -180 و 180.");
        }
    }

    private static ApiException Invalid(string english, string arabic) =>
        new(ErrorCodes.ExhibitorInvalid, 400, english, arabic);

    // Same-DB country FK — validated against the live Country table.
    private async Task EnsureCountryIsValidAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return; }
        var exists = await appDbContext.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == countryId.Value && country.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorInvalid, 400,
                $"Country id '{countryId}' does not exist or is inactive.",
                $"رقم البلد '{countryId}' غير موجود أو غير مفعّل.");
        }
    }

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
