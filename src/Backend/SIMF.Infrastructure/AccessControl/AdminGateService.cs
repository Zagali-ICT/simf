// Tests: SIMF.Api.Tests/Gates/AdminGatesTests.cs
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// D-148 — admin CRUD over <see cref="Gate"/> + assignments + allow-list +
/// reports. Mirrors <c>AdminHallService</c>; case-insensitive unique
/// <see cref="Gate.Code"/> via upper-case normalisation. Logical FKs
/// (ProfileTypeId, UserId) validated inline against the live tables before
/// write.
/// </summary>
internal sealed class AdminGateService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IAuditLog auditLog,
    IGateConfigCache configCache,
    TimeProvider timeProvider,
    ILogger<AdminGateService> logger) : IAdminGateService
{
    public async Task<GridPage<AdminGateSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = appDbContext.Gates.AsNoTracking().AsQueryable();

        // CP grid per-column filters (D-255). Unknown columns are ignored;
        // isActive is a status filter handled below. Code/Name/NameArabic
        // are server-side substring matches on App-owned columns.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "code":
                    rows = rows.Where(gate => gate.Code.Contains(v));
                    break;
                case "name":
                    rows = rows.Where(gate => gate.Name.Contains(v));
                    break;
                case "namearabic":
                    rows = rows.Where(gate => gate.NameArabic.Contains(v));
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(gate =>
                EF.Functions.Like(gate.Code, $"%{term}%")
                || EF.Functions.Like(gate.Name, $"%{term}%")
                || EF.Functions.Like(gate.NameArabic, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(gate => gate.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(gate => gate.Code),
            ("code", false) => rows.OrderBy(gate => gate.Code),
            ("name", true) => rows.OrderByDescending(gate => gate.Name),
            ("name", false) => rows.OrderBy(gate => gate.Name),
            ("namearabic", true) => rows.OrderByDescending(gate => gate.NameArabic),
            ("namearabic", false) => rows.OrderBy(gate => gate.NameArabic),
            ("directionmode", true) => rows.OrderByDescending(gate => gate.DirectionMode),
            ("directionmode", false) => rows.OrderBy(gate => gate.DirectionMode),
            ("createdat", true) => rows.OrderByDescending(gate => gate.CreatedAt),
            ("createdat", false) => rows.OrderBy(gate => gate.CreatedAt),
            _ => rows.OrderBy(gate => gate.Code),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(gate => new AdminGateSummary(
                gate.Id, gate.Code, gate.Name, gate.NameArabic,
                gate.DirectionMode,
                gate.AllowedProfileTypes.Count,
                gate.Assignments.Count(assignment => assignment.IsActive),
                gate.IsActive, gate.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminGateSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminGateDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var gate = await appDbContext.Gates.AsNoTracking()
            .Include(g => g.AllowedProfileTypes)
            .Include(g => g.Assignments.Where(a => a.IsActive))
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        return gate is null ? null : ToDetail(gate);
    }

    public async Task<AdminGateDetail> CreateAsync(
        Guid actorUserId, AdminCreateGateRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, name, nameArabic, description, descriptionArabic) =
            Validate(request.Code, request.Name, request.NameArabic,
                request.Description, request.DescriptionArabic);

        await ValidateProfileTypesAsync(request.AllowedProfileTypeIds, cancellationToken);
        await ValidateOperatorsAsync(request.AssignedOperatorUserIds, cancellationToken);

        var clash = await appDbContext.Gates.AsNoTracking()
            .AnyAsync(gate => gate.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(ErrorCodes.GateCodeDuplicate, 409,
                $"A gate with code '{code}' already exists.",
                $"توجد بوابة بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = code, Name = name, NameArabic = nameArabic,
            Description = description, DescriptionArabic = descriptionArabic,
            DirectionMode = request.DirectionMode,
            IsActive = true,
            CreatedAt = now,
        };
        foreach (var profileTypeId in request.AllowedProfileTypeIds.Distinct())
        {
            gate.AllowedProfileTypes.Add(new GateProfileTypeAllow
            {
                GateId = gate.Id,
                ProfileTypeId = profileTypeId,
            });
        }
        foreach (var userId in request.AssignedOperatorUserIds.Distinct())
        {
            gate.Assignments.Add(new GateAssignment
            {
                Id = Guid.NewGuid(),
                GateId = gate.Id,
                UserId = userId,
                IsActive = true,
                CreatedAt = now,
                CreateBy = actorUserId,
            });
        }

        appDbContext.Gates.Add(gate);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.GateCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={gate.Id}; code={code}; mode={request.DirectionMode}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Gate {Code} ({Id}) with {AllowCount} allow + {OpCount} operators",
            actorUserId, code, gate.Id,
            request.AllowedProfileTypeIds.Count, request.AssignedOperatorUserIds.Count);

        configCache.Invalidate(gate.Id);
        return ToDetail(gate);
    }

    public async Task<AdminGateDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateGateRequest request,
        CancellationToken cancellationToken = default)
    {
        var gate = await appDbContext.Gates
            .Include(g => g.AllowedProfileTypes)
            .Include(g => g.Assignments)
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.GateNotFound, 404,
                "The gate was not found.",
                "لم يتم العثور على البوابة.");

        var (code, name, nameArabic, description, descriptionArabic) =
            Validate(request.Code, request.Name, request.NameArabic,
                request.Description, request.DescriptionArabic);

        await ValidateProfileTypesAsync(request.AllowedProfileTypeIds, cancellationToken);
        await ValidateOperatorsAsync(request.AssignedOperatorUserIds, cancellationToken);

        if (!string.Equals(gate.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.Gates.AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.GateCodeDuplicate, 409,
                    $"A gate with code '{code}' already exists.",
                    $"توجد بوابة بالرمز '{code}' بالفعل.");
            }
        }

        gate.Code = code; gate.Name = name; gate.NameArabic = nameArabic;
        gate.Description = description; gate.DescriptionArabic = descriptionArabic;
        gate.DirectionMode = request.DirectionMode;
        gate.IsActive = request.IsActive;
        gate.UpdatedAt = timeProvider.GetUtcNow();

        SyncAllowedProfileTypes(gate, request.AllowedProfileTypeIds);
        SyncAssignments(gate, request.AssignedOperatorUserIds, actorUserId,
            timeProvider.GetUtcNow());

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.GateUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={gate.Id}; code={code}; active={gate.IsActive}",
        }, cancellationToken);

        configCache.Invalidate(gate.Id);
        return ToDetail(gate);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var gate = await appDbContext.Gates
            .SingleOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.GateNotFound, 404,
                "The gate was not found.",
                "لم يتم العثور على البوابة.");

        if (!gate.IsActive) { return; }

        gate.IsActive = false;
        gate.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.GateDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={gate.Id}; code={gate.Code}",
        }, cancellationToken);

        configCache.Invalidate(gate.Id);
    }

    public async Task<IReadOnlyList<AdminGateAssignmentRow>> ListAssignmentsAsync(
        Guid gateId, CancellationToken cancellationToken = default)
    {
        var assignments = await appDbContext.GateAssignments.AsNoTracking()
            .Where(a => a.GateId == gateId && a.IsActive)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0) { return Array.Empty<AdminGateAssignmentRow>(); }

        var operatorIds = assignments.Select(a => a.UserId).ToHashSet();
        var operatorNames = await identityDbContext.Users.AsNoTracking()
            .Where(u => operatorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName ?? string.Empty, cancellationToken);

        return assignments
            .Select(a => new AdminGateAssignmentRow(
                a.Id, a.UserId,
                operatorNames.TryGetValue(a.UserId, out var name) ? name : string.Empty,
                a.CreatedAt, a.CreateBy))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminGateScanRow>> ListScansAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default)
    {
        var top = Math.Clamp(filter.Top is > 0 ? filter.Top : 50, 1, 1000);
        var skip = Math.Max(0, filter.Skip);

        var query = appDbContext.GateScans.AsNoTracking().AsQueryable();

        if (filter.FromUtc is { } from) { query = query.Where(s => s.ScannedAtUtc >= from); }
        if (filter.ToUtc is { } to) { query = query.Where(s => s.ScannedAtUtc <= to); }
        if (filter.GateId is { } gateId) { query = query.Where(s => s.GateId == gateId); }
        if (filter.Outcome is { } outcome) { query = query.Where(s => s.Outcome == outcome); }

        var raw = await query
            .OrderByDescending(s => s.ScannedAtUtc)
            .Skip(skip).Take(top)
            .Join(appDbContext.Gates.AsNoTracking(),
                scan => scan.GateId, gate => gate.Id,
                (scan, gate) => new { scan, gateCode = gate.Code })
            .ToListAsync(cancellationToken);

        // D-167: UserProfile + SimfUser live in different DbContexts, so
        // resolving the display name is two round-trips — profile rows
        // first (App), then matching user rows (Identity), merge by id.
        var profileIds = raw
            .Where(r => r.scan.UserProfileId != null)
            .Select(r => r.scan.UserProfileId!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, string> displayNames;
        if (profileIds.Count == 0)
        {
            displayNames = new Dictionary<Guid, string>();
        }
        else
        {
            var profileUsers = await appDbContext.UserProfiles.AsNoTracking()
                .Where(profile => profileIds.Contains(profile.Id))
                .Select(profile => new { profile.Id, profile.UserId })
                .ToListAsync(cancellationToken);
            var userIds = profileUsers.Select(pu => pu.UserId).Distinct().ToList();
            var userNamesByUserId = await identityDbContext.Users.AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToDictionaryAsync(user => user.Id, user => user.DisplayName ?? string.Empty, cancellationToken);
            displayNames = profileUsers.ToDictionary(
                pu => pu.Id,
                pu => userNamesByUserId.TryGetValue(pu.UserId, out var name) ? name : string.Empty);
        }

        return raw.Select(r => new AdminGateScanRow(
                r.scan.Id, r.scan.GateId, r.gateCode,
                r.scan.UserProfileId,
                r.scan.UserProfileId is { } pid && displayNames.TryGetValue(pid, out var name)
                    ? name : null,
                r.scan.QrIdAtScan, r.scan.Direction, r.scan.Outcome,
                r.scan.DenialReasonCode, r.scan.ScannedAtUtc,
                r.scan.ScannedByUserId, r.scan.Source))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        CancellationToken cancellationToken = default)
    {
        // Per design notes §3.3 — most-recent allowed scan across all gates
        // per visitor; inside if CheckIn, outside if CheckOut or absent.
        var latest = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.Outcome == ScanOutcome.Allowed && s.UserProfileId != null)
            .GroupBy(s => s.UserProfileId!.Value)
            .Select(g => new
            {
                UserProfileId = g.Key,
                Last = g.OrderByDescending(s => s.ScannedAtUtc).First(),
            })
            .Where(x => x.Last.Direction == ScanDirection.CheckIn)
            .ToListAsync(cancellationToken);

        if (latest.Count == 0) { return Array.Empty<AdminCurrentlyInsideRow>(); }

        var gateIds = latest.Select(x => x.Last.GateId).Distinct().ToList();
        var gateCodes = await appDbContext.Gates.AsNoTracking()
            .Where(g => gateIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        // D-167: split cross-context join into App-then-Identity round-trips.
        var profileIds = latest.Select(x => x.UserProfileId).Distinct().ToList();
        var profileRows = await appDbContext.UserProfiles.AsNoTracking()
            .Include(profile => profile.ProfileType)
            .Where(profile => profileIds.Contains(profile.Id))
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.NameArabic,
                profile.ProfileTypeId,
                ProfileType = profile.ProfileType,
            })
            .ToListAsync(cancellationToken);
        var inProfileUserIds = profileRows.Select(pr => pr.UserId).Distinct().ToList();
        var profileUserDisplayNames = await identityDbContext.Users.AsNoTracking()
            .Where(user => inProfileUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName ?? string.Empty, cancellationToken);
        var profiles = profileRows.ToDictionary(
            row => row.Id,
            row => new
            {
                row.Id,
                DisplayName = profileUserDisplayNames.TryGetValue(row.UserId, out var name)
                    ? name : string.Empty,
                row.ArabicName,
                row.ProfileTypeId,
                ProfileType = row.ProfileType,
            });

        return latest
            .Where(x => profiles.ContainsKey(x.UserProfileId))
            .Select(x =>
            {
                var profile = profiles[x.UserProfileId];
                return new AdminCurrentlyInsideRow(
                    x.UserProfileId,
                    profile.DisplayName,
                    profile.ArabicName,
                    profile.ProfileTypeId,
                    profile.ProfileType?.Name,
                    profile.ProfileType?.PageColor,
                    x.Last.ScannedAtUtc,
                    x.Last.GateId,
                    gateCodes.TryGetValue(x.Last.GateId, out var code) ? code : string.Empty);
            })
            .OrderByDescending(row => row.LastCheckInAtUtc)
            .ToList();
    }

    public async Task<byte[]> ExportScansXlsxAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Top = Math.Clamp(filter.Top is > 0 ? filter.Top : 10_000, 1, 100_000);
        var rows = await ListScansAsync(filter, cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Scans");
        sheet.Cell(1, 1).Value = "Scan id";
        sheet.Cell(1, 2).Value = "Scanned at (UTC)";
        sheet.Cell(1, 3).Value = "Gate";
        sheet.Cell(1, 4).Value = "Visitor";
        sheet.Cell(1, 5).Value = "QR";
        sheet.Cell(1, 6).Value = "Direction";
        sheet.Cell(1, 7).Value = "Outcome";
        sheet.Cell(1, 8).Value = "Denial reason";
        sheet.Cell(1, 9).Value = "Source";

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rIdx = rowIndex + 2;
            sheet.Cell(rIdx, 1).Value = row.ScanId;
            sheet.Cell(rIdx, 2).Value = row.ScannedAtUtc.UtcDateTime.ToString("u",
                CultureInfo.InvariantCulture);
            sheet.Cell(rIdx, 3).Value = row.GateCode;
            sheet.Cell(rIdx, 4).Value = row.VisitorDisplayName ?? string.Empty;
            sheet.Cell(rIdx, 5).Value = row.QrIdAtScan;
            sheet.Cell(rIdx, 6).Value = row.Direction.ToString();
            sheet.Cell(rIdx, 7).Value = row.Outcome.ToString();
            sheet.Cell(rIdx, 8).Value = row.DenialReasonCode?.ToString() ?? string.Empty;
            sheet.Cell(rIdx, 9).Value = row.Source.ToString();
        }
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task ValidateProfileTypesAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) { return; }
        var distinct = ids.Distinct().ToList();
        var known = await appDbContext.ProfileTypes.AsNoTracking()
            .Where(p => distinct.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (known.Count != distinct.Count)
        {
            throw new ApiException(ErrorCodes.GateProfileTypeInvalid, 400,
                "One or more allowed profile types are missing or duplicated.",
                "أحد أنواع الملفات المسموح بها مفقود أو مكرر.");
        }
    }

    private async Task ValidateOperatorsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) { return; }
        var distinct = ids.Distinct().ToList();
        var known = await identityDbContext.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (known.Count != distinct.Count)
        {
            throw new ApiException(ErrorCodes.GateAssignmentInvalid, 400,
                "One or more assigned operators are missing or duplicated.",
                "أحد المشغلين المعينين مفقود أو مكرر.");
        }
    }

    private static void SyncAllowedProfileTypes(Gate gate, IReadOnlyList<Guid> next)
    {
        var distinct = next.Distinct().ToHashSet();
        var existing = gate.AllowedProfileTypes
            .Select(a => a.ProfileTypeId).ToHashSet();
        foreach (var row in gate.AllowedProfileTypes.ToList())
        {
            if (!distinct.Contains(row.ProfileTypeId))
            {
                gate.AllowedProfileTypes.Remove(row);
            }
        }
        foreach (var id in distinct)
        {
            if (!existing.Contains(id))
            {
                gate.AllowedProfileTypes.Add(new GateProfileTypeAllow
                {
                    GateId = gate.Id,
                    ProfileTypeId = id,
                });
            }
        }
    }

    private static void SyncAssignments(
        Gate gate, IReadOnlyList<Guid> next, Guid actorUserId, DateTimeOffset now)
    {
        var distinct = next.Distinct().ToHashSet();
        var activeByUser = gate.Assignments
            .Where(a => a.IsActive)
            .ToDictionary(a => a.UserId, a => a);

        foreach (var userId in activeByUser.Keys)
        {
            if (!distinct.Contains(userId))
            {
                var current = activeByUser[userId];
                current.IsActive = false;
                current.RevokedAt = now;
                current.RevokedByUserId = actorUserId;
            }
        }
        foreach (var userId in distinct)
        {
            if (!activeByUser.ContainsKey(userId))
            {
                gate.Assignments.Add(new GateAssignment
                {
                    Id = Guid.NewGuid(),
                    GateId = gate.Id,
                    UserId = userId,
                    IsActive = true,
                    CreatedAt = now,
                    CreateBy = actorUserId,
                });
            }
        }
    }

    private static (string code, string name, string nameArabic, string? description, string? descriptionArabic)
        Validate(string codeRaw, string nameRaw, string nameArabicRaw,
                 string? descriptionRaw, string? descriptionArabicRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(ErrorCodes.GateInvalid, 400,
                "Gate code must be between 2 and 16 characters.",
                "يجب أن يتراوح رمز البوابة بين 2 و 16 حرفاً.");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.GateInvalid, 400,
                "Gate English name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للبوابة بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.GateInvalid, 400,
                "Gate Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم العربي للبوابة بين 1 و 128 حرفاً.");
        }
        var description = string.IsNullOrWhiteSpace(descriptionRaw) ? null : descriptionRaw.Trim();
        if (description is { Length: > 1024 })
        {
            throw new ApiException(ErrorCodes.GateInvalid, 400,
                "Description must be 1024 characters or fewer.",
                "يجب أن يكون الوصف 1024 حرفاً أو أقل.");
        }
        var descriptionArabic = string.IsNullOrWhiteSpace(descriptionArabicRaw) ? null : descriptionArabicRaw.Trim();
        if (descriptionArabic is { Length: > 1024 })
        {
            throw new ApiException(ErrorCodes.GateInvalid, 400,
                "Arabic description must be 1024 characters or fewer.",
                "يجب أن يكون الوصف العربي 1024 حرفاً أو أقل.");
        }
        return (code, name, nameArabic, description, descriptionArabic);
    }

    private static AdminGateDetail ToDetail(Gate gate) =>
        new(gate.Id, gate.Code, gate.Name, gate.NameArabic,
            gate.Description, gate.DescriptionArabic,
            gate.DirectionMode, gate.IsActive,
            gate.AllowedProfileTypes.Select(a => a.ProfileTypeId).ToList(),
            gate.Assignments.Where(a => a.IsActive).Select(a => a.UserId).ToList(),
            gate.CreatedAt, gate.UpdatedAt);
}
