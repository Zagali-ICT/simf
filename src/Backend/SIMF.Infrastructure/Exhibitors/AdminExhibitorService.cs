// Tests: SIMF.Api.Tests/ExhibitorsTests.cs
// Tests: SIMF.Api.Tests/ExhibitorVisitorScanTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Exhibitors.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Exhibitors;
using SIMF.Domain.Exhibitors;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibitors;

/// <summary>Admin CRUD over exhibitors plus account provisioning.
/// Mirrors AdminDelegationService for the CRUD; account provisioning reuses the
/// existing admin provisioning pipeline
/// (<see cref="IAdminUserProvisioningService.CreateOtherAsync"/>) so we never
/// hand-roll UserManager — the provisioned account is a partner-side booth
/// officer carrying the exhibitor profile type (DEF-EXH-005), tagged to the
/// exhibitor via an ExhibitorMembership row.
/// <para>D-781 — <see cref="LinkAccountAsync"/> attaches an EXISTING account to an
/// exhibitor. Provisioning used to be the only writer of ExhibitorMembership, so
/// an exhibitor-typed account created through the generic Others pipeline had no
/// membership and was locked out of the booth tools (DEF-EXH-006) with no CP path
/// to attach it.</para></summary>
internal sealed class AdminExhibitorService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAdminUserProvisioningService provisioning,
    IAssetService assetService,
    SimfIdentityDbContext identityDbContext) : IAdminExhibitorService
{
    public async Task<GridPage<AdminExhibitorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = appDbContext.Exhibitors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(c =>
                EF.Functions.Like(c.Name, $"%{term}%")
                || EF.Functions.Like(c.NameArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-256). Unknown columns are ignored.
        // AccountCount is a computed sub-query, so it is not server-filterable.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "nameen":
                    rows = rows.Where(c => c.Name.Contains(v));
                    break;
                case "namear":
                    rows = rows.Where(c => c.NameArabic.Contains(v));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(c => c.IsActive == isActive);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-256). Default: NameAr.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("nameen", false) => rows.OrderBy(c => c.Name),
            ("nameen", true) => rows.OrderByDescending(c => c.Name),
            ("namear", true) => rows.OrderByDescending(c => c.NameArabic),
            ("isactive", false) => rows.OrderBy(c => c.IsActive),
            ("isactive", true) => rows.OrderByDescending(c => c.IsActive),
            _ => rows.OrderBy(c => c.NameArabic),
        };
        var total = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip(skip).Take(top)
            .Select(c => new
            {
                c.Id, c.Name, c.NameArabic,
                c.ContactEmail, c.ContactPhone, c.Website,
                AccountCount = appDbContext.Set<ExhibitorMembership>()
                    .Count(m => m.ExhibitorId == c.Id && m.IsActive),
                c.IsActive, c.CreatedAt, c.Tier,
            })
            .ToListAsync(cancellationToken);

        // The exhibitor now also owns its own ExhibitorLogo (the app + the grid
        // render this, not the linked Contact's) — one batched query over the
        // page's exhibitor ids.
        var exhibitorLogoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.ExhibitorLogo, pageRows.Select(row => row.Id).ToList(), cancellationToken);

        var page = pageRows
            .Select(c => new AdminExhibitorSummary(
                c.Id, c.Name, c.NameArabic,
                c.ContactEmail, c.ContactPhone, c.Website,
                c.AccountCount,
                c.IsActive, c.CreatedAt, c.Tier,
                exhibitorLogoOwners.Contains(c.Id)))
            .ToList();

        return GridPage<AdminExhibitorSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminExhibitorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.Exhibitors.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new AdminExhibitorDetail(
                c.Id, c.Name, c.NameArabic,
                c.ContactEmail, c.ContactPhone, c.Website,
                c.IsActive, c.CreatedAt, c.UpdatedAt, c.Tier,
                c.CountryId,
                c.Country != null ? c.Country.Name : null,
                c.Country != null ? c.Country.NameArabic : null,
                c.PhoneSecondary, c.FacebookUrl, c.XUrl, c.LinkedInUrl,
                c.InstagramUrl, c.City, c.CityArabic,
                c.Latitude, c.Longitude))
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
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
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
        exhibitor.IsActive = request.IsActive;
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
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        if (!exhibitor.IsActive) { return; }
        exhibitor.IsActive = false;
        exhibitor.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ExhibitorDeactivated,
            actorUserId,
            $"exhibitorId={exhibitor.Id}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ExhibitorAccountSummary>> ListAccountsAsync(
        Guid exhibitorId, CancellationToken cancellationToken = default)
    {
        // Confirm the exhibitor exists so a stranger id 404s instead of
        // silently returning an empty list.
        var exists = await appDbContext.Exhibitors
            .AsNoTracking()
            .AnyAsync(c => c.Id == exhibitorId, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.ExhibitorNotFound, 404,
                "Exhibitor not found.",
                "لم يتم العثور على العارض.");
        }

        var memberships = await appDbContext.Set<ExhibitorMembership>()
            .AsNoTracking()
            .Where(m => m.ExhibitorId == exhibitorId && m.IsActive)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                m.ContactName,
                m.RoleLabel,
                m.IsActive,
                m.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            return Array.Empty<ExhibitorAccountSummary>();
        }

        // Resolve the account emails cross-context (UserId is a logical FK to
        // SimfUser on the Identity DB — no DB-level JOIN is possible, so read
        // the small id set back AsNoTracking).
        var userIds = memberships.Select(m => m.UserId).ToList();
        var emailsById = await identityDbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        return memberships
            .Select(m => new ExhibitorAccountSummary(
                m.Id,
                m.UserId,
                m.ContactName,
                emailsById.TryGetValue(m.UserId, out var email) ? email ?? string.Empty : string.Empty,
                m.RoleLabel,
                m.IsActive,
                m.CreatedAt))
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
            ContactName = contactName,
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
            membership.ContactName,
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
    /// The scanner-side controls themselves are unchanged (owner decision D-780
    /// widened the SUBJECT rule only): the caller must still be exhibitor-typed AND
    /// hold a live membership, so revoking a membership still revokes the tools.</para>
    ///
    /// <para>The account must already carry an active exhibitor-mapped profile type
    /// — linking deliberately does not mutate the account's profile type, because
    /// that silently changes the app role a different admin assigned. An admin sets
    /// the type on the Others page and then links here.</para>
    ///
    /// <para>D-157 — the account lives on the Identity DB and the membership on the
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
        // (ProfileType.MobileAppRole, D-519). Linking an account that does not
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
        var contactName = contactNameOverride
            ?? DefaultContactName(account.DisplayName, accountEmail);

        var link = new ExhibitorMembership
        {
            Id = Guid.NewGuid(),
            ExhibitorId = exhibitor.Id,
            UserId = account.Id,
            ContactName = contactName,
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
            link.ContactName,
            accountEmail,
            link.RoleLabel,
            link.IsActive,
            link.CreatedAt);
    }

    /// <summary>The membership contact name when the admin leaves the field
    /// blank: the account's display name, else its login email. Capped at the
    /// column's 256 characters so a pathological address cannot fail the insert
    /// (an email may be up to 320).</summary>
    private static string DefaultContactName(string displayName, string email)
    {
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
            .SingleOrDefaultAsync(c => c.Id == exhibitorId, cancellationToken)
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
    /// lead-capture endpoints authorise on (D-519). Partner-side by definition
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
        string? phoneSecondary, string? facebook, string? x,
        string? linkedIn, string? instagram, string? city, string? cityArabic,
        double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(phoneSecondary) && phoneSecondary.Length > 32)
        {
            throw Invalid("Phone numbers must be 32 characters or less.",
                "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
        }
        foreach (var url in new[] { facebook, x, linkedIn, instagram })
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
