// Tests: SIMF.Api.Tests/DelegationsTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.Delegations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Delegations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Delegations;

/// <summary>D-174 (gap doc G11, Mockup page 21) — admin CRUD over
/// delegations.</summary>
internal sealed class AdminDelegationService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IAdminDelegationService
{
    public async Task<GridPage<AdminDelegationSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.Delegations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(d =>
                EF.Functions.Like(d.Name, $"%{term}%")
                || EF.Functions.Like(d.NameArabic, $"%{term}%"));
        }

        // CP grid per-column filters (D-256). Unknown columns are ignored.
        // The Country column resolves through the same-DB Country navigation
        // (Countries is an App table, not Identity) so it is EF-translatable.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "name":
                    rows = rows.Where(d => d.Name.Contains(v));
                    break;
                case "namearabic":
                    rows = rows.Where(d => d.NameArabic.Contains(v));
                    break;
                case "country":
                    rows = rows.Where(d => d.Country != null
                        && (d.Country.NameEn.Contains(v) || d.Country.NameAr.Contains(v)));
                    break;
                case "isactive":
                    if (bool.TryParse(v, out var isActive))
                    {
                        rows = rows.Where(d => d.IsActive == isActive);
                    }
                    break;
                case "ispriority":
                    if (bool.TryParse(v, out var isPriority))
                    {
                        rows = rows.Where(d => d.IsPriority == isPriority);
                    }
                    break;
            }
        }

        // CP grid sortable columns (D-256). Default preserves the public order:
        // DisplayOrder asc, then NameArabic asc.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", false) => rows.OrderBy(d => d.Name),
            ("name", true) => rows.OrderByDescending(d => d.Name),
            ("namearabic", true) => rows.OrderByDescending(d => d.NameArabic),
            ("namearabic", false) => rows.OrderBy(d => d.NameArabic),
            ("country", false) => rows.OrderBy(d => d.Country != null ? d.Country.NameEn : null),
            ("country", true) => rows.OrderByDescending(d => d.Country != null ? d.Country.NameEn : null),
            ("membercount", false) => rows.OrderBy(d => d.MemberCount),
            ("membercount", true) => rows.OrderByDescending(d => d.MemberCount),
            ("ispriority", false) => rows.OrderBy(d => d.IsPriority),
            ("ispriority", true) => rows.OrderByDescending(d => d.IsPriority),
            ("isinternational", false) => rows.OrderBy(d => d.IsInternational),
            ("isinternational", true) => rows.OrderByDescending(d => d.IsInternational),
            ("displayorder", true) => rows.OrderByDescending(d => d.DisplayOrder).ThenBy(d => d.NameArabic),
            ("isactive", false) => rows.OrderBy(d => d.IsActive),
            ("isactive", true) => rows.OrderByDescending(d => d.IsActive),
            _ => rows.OrderBy(d => d.DisplayOrder).ThenBy(d => d.NameArabic),
        };
        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(d => new AdminDelegationSummary(
                d.Id, d.Name, d.NameArabic, d.CountryId,
                d.Country != null ? d.Country.NameEn : null,
                d.Country != null ? d.Country.NameAr : null,
                d.MemberCount, d.IsPriority, d.IsInternational,
                d.DisplayOrder, d.IsActive, d.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminDelegationSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminDelegationSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await appDbContext.Delegations.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new AdminDelegationSummary(
                d.Id, d.Name, d.NameArabic, d.CountryId,
                d.Country != null ? d.Country.NameEn : null,
                d.Country != null ? d.Country.NameAr : null,
                d.MemberCount, d.IsPriority, d.IsInternational,
                d.DisplayOrder, d.IsActive, d.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminDelegationSummary> CreateAsync(
        Guid actorUserId, CreateDelegationRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.NameArabic, request.MemberCount,
            request.DisplayOrder);
        var now = timeProvider.GetUtcNow();
        var delegation = new Delegation
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NameArabic = request.NameArabic.Trim(),
            CountryId = request.CountryId,
            MemberCount = request.MemberCount,
            IsPriority = request.IsPriority,
            IsInternational = request.IsInternational,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        appDbContext.Delegations.Add(delegation);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"delegationId={delegation.Id}; name={delegation.NameArabic}",
        }, cancellationToken);

        return (await GetAsync(delegation.Id, cancellationToken))!;
    }

    public async Task<AdminDelegationSummary> UpdateAsync(
        Guid actorUserId, Guid id, UpdateDelegationRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request.Name, request.NameArabic, request.MemberCount,
            request.DisplayOrder);
        var delegation = await appDbContext.Delegations
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.DelegationNotFound, 404,
                "Delegation not found.",
                "لم يتم العثور على الوفد.");

        delegation.Name = request.Name.Trim();
        delegation.NameArabic = request.NameArabic.Trim();
        delegation.CountryId = request.CountryId;
        delegation.MemberCount = request.MemberCount;
        delegation.IsPriority = request.IsPriority;
        delegation.IsInternational = request.IsInternational;
        delegation.DisplayOrder = request.DisplayOrder;
        delegation.IsActive = request.IsActive;
        delegation.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"delegationId={delegation.Id}; active={delegation.IsActive}",
        }, cancellationToken);

        return (await GetAsync(delegation.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var delegation = await appDbContext.Delegations
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.DelegationNotFound, 404,
                "Delegation not found.",
                "لم يتم العثور على الوفد.");
        if (!delegation.IsActive) { return; }
        delegation.IsActive = false;
        delegation.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.DelegationDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"delegationId={delegation.Id}",
        }, cancellationToken);
    }

    private static void Validate(string name, string nameArabic,
        int memberCount, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256
            || string.IsNullOrWhiteSpace(nameArabic) || nameArabic.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.DelegationInvalid, 400,
                "Delegation name (EN + AR) must be between 1 and 256 characters.",
                "يجب أن يتراوح طول اسم الوفد (إنجليزي + عربي) بين 1 و 256 حرفاً.");
        }
        if (memberCount < 0)
        {
            throw new ApiException(
                ErrorCodes.DelegationInvalid, 400,
                "Member count must be zero or positive.",
                "يجب أن يكون عدد الأعضاء صفراً أو موجباً.");
        }
        if (displayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.DelegationInvalid, 400,
                "Display order must be zero or positive.",
                "يجب أن يكون ترتيب العرض صفراً أو موجباً.");
        }
    }
}
