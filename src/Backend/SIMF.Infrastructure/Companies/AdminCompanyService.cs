// Tests: SIMF.Api.Tests/CompaniesTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Companies.Abstractions;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Companies;
using SIMF.Domain.Companies;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Companies;

/// <summary>D-199 #3 — admin CRUD over exhibitor / sponsor companies plus
/// account provisioning. Mirrors AdminDelegationService for the CRUD; account
/// provisioning reuses the existing admin provisioning pipeline
/// (<see cref="IAdminUserProvisioningService.CreateVisitorAsync"/>) so we never
/// hand-roll UserManager — the provisioned account is a least-privilege
/// Visitor tagged to the company via a CompanyMembership row.</summary>
internal sealed class AdminCompanyService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IAdminUserProvisioningService provisioning,
    SimfIdentityDbContext identityDbContext) : IAdminCompanyService
{
    public async Task<GridPage<AdminCompanySummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.Companies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(c =>
                EF.Functions.Like(c.NameEn, $"%{term}%")
                || EF.Functions.Like(c.NameAr, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(c => c.IsActive == isActive);
        }
        if (query.Filters.TryGetValue("type", out var typeFilter)
            && Enum.TryParse<CompanyType>(typeFilter, ignoreCase: true, out var parsedType))
        {
            rows = rows.Where(c => c.Type == parsedType);
        }

        rows = rows.OrderBy(c => c.Type).ThenBy(c => c.NameAr);
        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(c => new AdminCompanySummary(
                c.Id, c.NameEn, c.NameAr, (int)c.Type,
                c.ContactEmail, c.ContactPhone, c.Website,
                appDbContext.Set<CompanyMembership>()
                    .Count(m => m.CompanyId == c.Id && m.IsActive),
                c.IsActive, c.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminCompanySummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminCompanyDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.Companies.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new AdminCompanyDetail(
                c.Id, c.NameEn, c.NameAr, (int)c.Type,
                c.ContactEmail, c.ContactPhone, c.Website,
                c.IsActive, c.CreatedAt, c.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminCompanyDetail> CreateAsync(
        Guid actorUserId, CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = ParseType(request.Type);
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website);
        var now = timeProvider.GetUtcNow();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            Type = type,
            ContactEmail = NormaliseOptional(request.ContactEmail),
            ContactPhone = NormaliseOptional(request.ContactPhone),
            Website = NormaliseOptional(request.Website),
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Companies.Add(company);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CompanyCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"companyId={company.Id}; type={company.Type}; name={company.NameAr}",
        }, cancellationToken);

        return (await GetAsync(company.Id, cancellationToken))!;
    }

    public async Task<AdminCompanyDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = ParseType(request.Type);
        Validate(request.NameEn, request.NameAr, request.ContactEmail,
            request.ContactPhone, request.Website);
        var company = await appDbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.CompanyNotFound, 404,
                "Company not found.",
                "لم يتم العثور على الشركة.");

        company.NameEn = request.NameEn.Trim();
        company.NameAr = request.NameAr.Trim();
        company.Type = type;
        company.ContactEmail = NormaliseOptional(request.ContactEmail);
        company.ContactPhone = NormaliseOptional(request.ContactPhone);
        company.Website = NormaliseOptional(request.Website);
        company.IsActive = request.IsActive;
        company.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CompanyUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"companyId={company.Id}; active={company.IsActive}",
        }, cancellationToken);

        return (await GetAsync(company.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var company = await appDbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.CompanyNotFound, 404,
                "Company not found.",
                "لم يتم العثور على الشركة.");
        if (!company.IsActive) { return; }
        company.IsActive = false;
        company.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CompanyDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"companyId={company.Id}",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyAccountSummary>> ListAccountsAsync(
        Guid companyId, CancellationToken cancellationToken = default)
    {
        // Confirm the company exists so a stranger id 404s instead of
        // silently returning an empty list.
        var exists = await appDbContext.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == companyId, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.CompanyNotFound, 404,
                "Company not found.",
                "لم يتم العثور على الشركة.");
        }

        var memberships = await appDbContext.Set<CompanyMembership>()
            .AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.IsActive)
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
            return Array.Empty<CompanyAccountSummary>();
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
            .Select(m => new CompanyAccountSummary(
                m.Id,
                m.UserId,
                m.ContactName,
                emailsById.TryGetValue(m.UserId, out var email) ? email ?? string.Empty : string.Empty,
                m.RoleLabel,
                m.IsActive,
                m.CreatedAt))
            .ToList();
    }

    public async Task<CompanyAccountSummary> ProvisionAccountAsync(
        Guid actorUserId, Guid companyId, ProvisionCompanyAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var company = await appDbContext.Companies
            .SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.CompanyNotFound, 404,
                "Company not found.",
                "لم يتم العثور على الشركة.");
        if (!company.IsActive)
        {
            throw new ApiException(
                ErrorCodes.CompanyInactive, 409,
                "The company is not active; reactivate it before adding accounts.",
                "الشركة غير نشطة؛ يرجى إعادة تفعيلها قبل إضافة الحسابات.");
        }

        var contactName = (request.ContactName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        if (contactName.Length is 0 or > 256 || email.Length is 0 or > 320)
        {
            throw new ApiException(
                ErrorCodes.CompanyAccountInvalid, 400,
                "Contact name (1-256) and email (1-320) are required.",
                "اسم جهة الاتصال (1-256) والبريد الإلكتروني (1-320) مطلوبان.");
        }
        var roleLabel = NormaliseOptional(request.RoleLabel);
        if (roleLabel is { Length: > 128 })
        {
            throw new ApiException(
                ErrorCodes.CompanyAccountInvalid, 400,
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
        var membership = new CompanyMembership
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            UserId = created.UserId,
            ContactName = contactName,
            RoleLabel = roleLabel,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Set<CompanyMembership>().Add(membership);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CompanyAccountProvisioned,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = created.UserId,
            SubjectEmail = created.Email,
            Detail = $"companyId={company.Id}; membershipId={membership.Id}",
        }, cancellationToken);

        return new CompanyAccountSummary(
            membership.Id,
            membership.UserId,
            membership.ContactName,
            created.Email,
            membership.RoleLabel,
            membership.IsActive,
            membership.CreatedAt);
    }

    private static CompanyType ParseType(int type)
    {
        if (!Enum.IsDefined(typeof(CompanyType), type))
        {
            throw new ApiException(
                ErrorCodes.CompanyInvalid, 400,
                "Company type is not valid.",
                "نوع الشركة غير صالح.");
        }
        return (CompanyType)type;
    }

    private static void Validate(
        string nameEn, string nameAr, string? contactEmail,
        string? contactPhone, string? website)
    {
        if (string.IsNullOrWhiteSpace(nameEn) || nameEn.Length > 256
            || string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.CompanyInvalid, 400,
                "Company name (EN + AR) must be between 1 and 256 characters.",
                "يجب أن يتراوح طول اسم الشركة (إنجليزي + عربي) بين 1 و 256 حرفاً.");
        }
        if (contactEmail is { Length: > 320 })
        {
            throw new ApiException(
                ErrorCodes.CompanyInvalid, 400,
                "Contact email must be 320 characters or fewer.",
                "يجب ألا يتجاوز البريد الإلكتروني 320 حرفاً.");
        }
        if (contactPhone is { Length: > 32 })
        {
            throw new ApiException(
                ErrorCodes.CompanyInvalid, 400,
                "Contact phone must be 32 characters or fewer.",
                "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
        }
        if (website is { Length: > 512 })
        {
            throw new ApiException(
                ErrorCodes.CompanyInvalid, 400,
                "Website must be 512 characters or fewer.",
                "يجب ألا يتجاوز الموقع الإلكتروني 512 حرفاً.");
        }
    }

    private static string? NormaliseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
