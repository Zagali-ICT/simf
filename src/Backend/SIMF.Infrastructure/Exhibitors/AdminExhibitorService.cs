// Tests: SIMF.Api.Tests/ExhibitorsTests.cs
using Microsoft.EntityFrameworkCore;
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

/// <summary>D-199 #3 — admin CRUD over exhibitors plus account provisioning.
/// Mirrors AdminDelegationService for the CRUD; account provisioning reuses the
/// existing admin provisioning pipeline
/// (<see cref="IAdminUserProvisioningService.CreateVisitorAsync"/>) so we never
/// hand-roll UserManager — the provisioned account is a least-privilege
/// Visitor tagged to the exhibitor via an ExhibitorMembership row.</summary>
internal sealed class AdminExhibitorService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAdminUserProvisioningService provisioning,
    SimfIdentityDbContext identityDbContext) : IAdminExhibitorService
{
    public async Task<GridPage<AdminExhibitorSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

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
        var page = await rows
            .Skip(skip).Take(top)
            .Select(c => new AdminExhibitorSummary(
                c.Id, c.Name, c.NameArabic,
                c.ContactEmail, c.ContactPhone, c.Website,
                appDbContext.Set<ExhibitorMembership>()
                    .Count(m => m.ExhibitorId == c.Id && m.IsActive),
                c.IsActive, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminExhibitorSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminExhibitorDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.Exhibitors.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new AdminExhibitorDetail(
                c.Id, c.Name, c.NameArabic,
                c.ContactEmail, c.ContactPhone, c.Website,
                c.IsActive, c.CreatedAt, c.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminExhibitorDetail> CreateAsync(
        Guid actorUserId, CreateExhibitorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website);
        var now = timeProvider.GetUtcNow();
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = request.NameEn.Trim(),
            NameArabic = request.NameAr.Trim(),
            ContactEmail = NormaliseOptional(request.ContactEmail),
            ContactPhone = NormaliseOptional(request.ContactPhone),
            Website = NormaliseOptional(request.Website),
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Exhibitors.Add(exhibitor);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ExhibitorCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"exhibitorId={exhibitor.Id}; name={exhibitor.NameArabic}",
        }, cancellationToken);

        return (await GetAsync(exhibitor.Id, cancellationToken))!;
    }

    public async Task<AdminExhibitorDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateExhibitorRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website);
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
        exhibitor.IsActive = request.IsActive;
        exhibitor.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ExhibitorUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"exhibitorId={exhibitor.Id}; active={exhibitor.IsActive}",
        }, cancellationToken);

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
        exhibitor.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ExhibitorDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"exhibitorId={exhibitor.Id}",
        }, cancellationToken);
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

        // Reuse the existing admin provisioning pipeline — a least-privilege
        // Visitor account (no ProfileTypeId, no RBAC role). It validates the
        // email-already-registered case and throws ApiException on conflict.
        var created = await provisioning.CreateVisitorAsync(
            actorUserId,
            new AdminCreateVisitorRequest
            {
                Email = email,
                DisplayName = contactName,
                ProfileTypeId = null,
            },
            cancellationToken);

        var now = timeProvider.GetUtcNow();
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

    private static void Validate(
        string nameEn, string nameAr, string? contactEmail,
        string? contactPhone, string? website)
    {
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

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
