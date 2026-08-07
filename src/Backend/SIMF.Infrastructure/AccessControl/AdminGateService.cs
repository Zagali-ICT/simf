// Tests: SIMF.Api.Tests/Gates/AdminGatesTests.cs
//        SIMF.Api.Tests/GateOperatorModelTests.cs (operator candidates,
//        operator eligibility validation, gate-form lookups, assignment email)
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.AccessControl;

/// <summary>
/// Admin CRUD over <see cref="Gate"/> + assignments + allow-list +
/// reports. Mirrors <c>AdminHallService</c>; case-insensitive unique
/// <see cref="Gate.Code"/> via upper-case normalisation. Logical FKs
/// (ProfileTypeId, UserId) validated inline against the live tables before
/// write.
/// </summary>
internal sealed class AdminGateService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IIdentityUserDirectory userDirectory,
    IAuditLog auditLog,
    IGateConfigCache configCache,
    TimeProvider timeProvider,
    ILogger<AdminGateService> logger) : IAdminGateService
{
    /// <summary>How long after a visitor's last allowed check-in (with no
    /// later scan) they are still counted as "currently inside". Bounds the
    /// occupancy view against In-only gates that never emit a CheckOut.</summary>
    private static readonly TimeSpan StalePresenceWindow = TimeSpan.FromHours(16);

    /// <summary>The <c>ProfileType.MobileAppRole</c> values that actually
    /// confer gate operation. Derived from the permission catalogue (the roles whose
    /// operational grant set carries <c>Gates.Operate</c> — Staff and Moderator) so
    /// the eligibility rule can never drift from the permission model.</summary>
    private static readonly MobileAppRole[] GateOperatorAppRoles =
        Enum.GetValues<MobileAppRole>()
            .Where(role => PermissionCatalog.OperationalPermissionsForAppRole(role)
                .Contains(PermissionCatalog.Gates.Operate))
            .ToArray();

    public async Task<GridPage<AdminGateSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = appDbContext.Gates.AsNoTracking().AsQueryable();

        // CP grid per-column filters. Unknown columns are ignored;
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
                gate.IsActive, gate.CreatedAt,
                // Carried so the grid Excel export round-trips the
                // bilingual description (positional order matches the record).
                gate.Description, gate.DescriptionArabic))
            .ToListAsync(cancellationToken);

        return GridPage<AdminGateSummary>.Of(page, total,
            skip, top);
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
        await ValidateHallAsync(request.HallId, cancellationToken);

        var clash = await appDbContext.Gates.AsNoTracking()
            .AnyAsync(gate => gate.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(ErrorCodes.GateCodeDuplicate, 409,
                $"A gate with code '{code}' already exists.",
                $"توجد بوابة بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var gate = new Gate
        {
            Id = Guid.NewGuid(),
            Code = code, Name = name, NameArabic = nameArabic,
            Description = description, DescriptionArabic = descriptionArabic,
            DirectionMode = request.DirectionMode,
            HallId = request.HallId,
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.GateCreated,
            actorUserId,
            $"id={gate.Id}; code={code}; mode={request.DirectionMode}",
            cancellationToken);

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
        await ValidateHallAsync(request.HallId, cancellationToken);

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
        gate.HallId = request.HallId;
        gate.IsActive = request.IsActive;
        gate.UpdatedAt = timeProvider.SimfNow();

        SyncAllowedProfileTypes(gate, request.AllowedProfileTypeIds);
        SyncAssignments(gate, request.AssignedOperatorUserIds, actorUserId,
            timeProvider.SimfNow());

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.GateUpdated,
            actorUserId,
            $"id={gate.Id}; code={code}; active={gate.IsActive}",
            cancellationToken);

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
        gate.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.GateDeactivated,
            actorUserId,
            $"id={gate.Id}; code={gate.Code}",
            cancellationToken);

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
        var operatorNames = await userDirectory.GetDisplayNamesAsync(
            operatorIds, cancellationToken);
        // The CP detail view lists name + email per operator.
        var operatorEmails = await userDirectory.GetEmailsAsync(
            operatorIds, cancellationToken);

        return assignments
            .Select(a => new AdminGateAssignmentRow(
                a.Id, a.UserId,
                operatorNames.TryGetValue(a.UserId, out var name) ? name : string.Empty,
                a.CreatedAt, a.CreateBy,
                operatorEmails.TryGetValue(a.UserId, out var email) && email is not null
                    ? email : string.Empty))
            .ToList();
    }

    /// <summary>The candidate gate operators. Gate scanning
    /// happens through the mobile app, so a candidate is an approved APP account
    /// whose profile type is operational (<c>IsForVisitor=false</c>) and carries a
    /// MobileAppRole that confers <c>Gates.Operate</c>. Deactivated / pending /
    /// rejected accounts are excluded and the list is searchable + paged.
    /// Resolved as three reads across the DB split (App → App → Identity), merged
    /// in memory — never a cross-database JOIN.</summary>
    public async Task<GridPage<AdminGateOperatorCandidate>> ListOperatorCandidatesAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        // Step 1 (App DB) — the operational profile types that confer gate work.
        var operatorProfileTypes = await appDbContext.ProfileTypes.AsNoTracking()
            .Where(profileType => profileType.IsActive
                && !profileType.IsForVisitor
                && GateOperatorAppRoles.Contains(profileType.MobileAppRole))
            .Select(profileType => new
            {
                profileType.Id,
                profileType.Name,
                profileType.MobileAppRole,
            })
            .ToListAsync(cancellationToken);
        if (operatorProfileTypes.Count == 0)
        {
            return GridPage<AdminGateOperatorCandidate>.Of(
                Array.Empty<AdminGateOperatorCandidate>(), 0, skip, top);
        }

        // Step 2 (App DB) — the profile rows carrying one of those types.
        var profileTypeIds = operatorProfileTypes.Select(row => row.Id).ToList();
        var profileRows = await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.ProfileTypeId != null
                && profileTypeIds.Contains(profile.ProfileTypeId.Value))
            .Select(profile => new
            {
                profile.UserId,
                ProfileTypeId = profile.ProfileTypeId!.Value,
            })
            .ToListAsync(cancellationToken);
        if (profileRows.Count == 0)
        {
            return GridPage<AdminGateOperatorCandidate>.Of(
                Array.Empty<AdminGateOperatorCandidate>(), 0, skip, top);
        }

        var profileTypeByUserId = profileRows
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.First().ProfileTypeId);
        var profileTypeNames = operatorProfileTypes.ToDictionary(
            row => row.Id, row => (row.Name, row.MobileAppRole));

        // Step 3 (Identity DB) — only approved app accounts; search + page there so
        // the totals are server-side and the picker is never a blind top-200.
        var candidateUserIds = profileTypeByUserId.Keys.ToList();
        var accounts = identityDbContext.Users.AsNoTracking()
            .Where(user => candidateUserIds.Contains(user.Id)
                && user.UserType == UserType.Visitor
                && user.AccountState == AccountState.Approved);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            accounts = accounts.Where(user =>
                (user.Email != null && EF.Functions.Like(user.Email, $"%{term}%"))
                || EF.Functions.Like(user.DisplayName, $"%{term}%"));
        }

        var total = await accounts.CountAsync(cancellationToken);
        var page = await accounts
            .OrderBy(user => user.DisplayName)
            .Skip(skip).Take(top)
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .ToListAsync(cancellationToken);

        var rows = page
            .Select(user =>
            {
                var profileTypeId = profileTypeByUserId[user.Id];
                var (name, role) = profileTypeNames[profileTypeId];
                return new AdminGateOperatorCandidate(
                    user.Id, user.Email ?? string.Empty, user.DisplayName, name, role);
            })
            .ToList();

        return GridPage<AdminGateOperatorCandidate>.Of(rows, total, skip, top);
    }

    /// <summary>The gate form's own lookups. The form used to read
    /// the shared ProfileTypes / Halls admin lists, which need
    /// <c>ProfileTypes.View</c> / <c>Halls.View</c>; a Security-team gate manager
    /// holds only <c>Gates.Manage</c> and therefore saw silently empty dropdowns.
    /// Both lists live in the App DB — one context, no cross-database read.</summary>
    public async Task<AdminGateFormOptions> GetFormOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var profileTypes = await appDbContext.ProfileTypes.AsNoTracking()
            .Where(profileType => profileType.IsActive)
            .OrderBy(profileType => profileType.Name)
            .Select(profileType => new AdminGateLookupOption(
                profileType.Id, profileType.Name, profileType.NameArabic))
            .ToListAsync(cancellationToken);

        var halls = await appDbContext.Halls.AsNoTracking()
            .Where(hall => hall.IsActive)
            .OrderBy(hall => hall.Name)
            .Select(hall => new AdminGateLookupOption(
                hall.Id, hall.Name, hall.NameArabic))
            .ToListAsync(cancellationToken);

        return new AdminGateFormOptions(profileTypes, halls);
    }

    public async Task<IReadOnlyList<AdminGateScanRow>> ListScansAsync(
        AdminGateScanReportFilter filter, CancellationToken cancellationToken = default)
    {
        var top = Math.Clamp(filter.Top is > 0 ? filter.Top : 50, 1, 1000);
        var skip = Math.Max(0, filter.Skip);

        var query = appDbContext.GateScans.AsNoTracking().AsQueryable();

        if (filter.FromUtc is { } from) { query = query.Where(s => s.ScannedAt >= from); }
        if (filter.ToUtc is { } to) { query = query.Where(s => s.ScannedAt <= to); }
        if (filter.GateId is { } gateId) { query = query.Where(s => s.GateId == gateId); }
        if (filter.Outcome is { } outcome) { query = query.Where(s => s.Outcome == outcome); }

        var raw = await query
            .OrderByDescending(s => s.ScannedAt)
            .Skip(skip).Take(top)
            .Join(appDbContext.Gates.AsNoTracking(),
                scan => scan.GateId, gate => gate.Id,
                (scan, gate) => new { scan, gateCode = gate.Code })
            .ToListAsync(cancellationToken);

        // UserProfile + SimfUser live in different DbContexts, so
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
            var userNamesByUserId = await userDirectory.GetDisplayNamesAsync(
                userIds, cancellationToken);
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
                r.scan.DenialReasonCode, r.scan.ScannedAt,
                r.scan.ScannedByUserId, r.scan.Source))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        CancellationToken cancellationToken = default)
    {
        // Per design notes §3.3 — most-recent allowed scan across all gates
        // per visitor; inside if CheckIn, outside if CheckOut or absent.
        // In-only gates never emit a CheckOut, so a bare "latest scan is a
        // CheckIn" counts a visitor as inside forever. Bound presence to a rolling
        // window: a check-in older than StalePresenceWindow with no later scan is
        // treated as departed (day/session-boundary reconciliation).
        var presenceCutoff = timeProvider.SimfNow() - StalePresenceWindow;

        // "Latest allowed scan per visitor" expressed as a correlated NOT EXISTS,
        // NOT as a filter over a GroupBy projection. The previous form —
        //
        //     .GroupBy(s => s.UserProfileId!.Value)
        //     .Select(g => new { UserProfileId = g.Key,
        //                        Last = g.OrderByDescending(s => s.ScannedAt).First() })
        //     .Where(x => x.Last.Direction == ScanDirection.CheckIn
        //              && x.Last.ScannedAt >= presenceCutoff)
        //
        // cannot be translated: filtering on a member of an entity projected out of
        // a grouping makes EF throw KeyNotFoundException('EmptyProjectionMember')
        // while it builds the SQL. That happens at translation time, so the endpoint
        // returned 500 on EVERY request — an empty GateScans table included, which
        // is why no amount of seeding would have shown it. It shipped because
        // nothing executed this method against a relational provider; the regression
        // test added with this fix does (AdminGateCurrentlyInsideTests).
        //
        // The Id tiebreak keeps exactly one row per visitor when two scans share a
        // ScannedAt — the guarantee OrderByDescending(...).First() gave for free.
        var latest = await appDbContext.GateScans.AsNoTracking()
            .Where(s => s.Outcome == ScanOutcome.Allowed
                && s.UserProfileId != null
                && s.Direction == ScanDirection.CheckIn
                && s.ScannedAt >= presenceCutoff
                && !appDbContext.GateScans.Any(later =>
                    later.UserProfileId == s.UserProfileId
                    && later.Outcome == ScanOutcome.Allowed
                    && (later.ScannedAt > s.ScannedAt
                        || (later.ScannedAt == s.ScannedAt && later.Id > s.Id))))
            .Select(s => new { UserProfileId = s.UserProfileId!.Value, Last = s })
            .ToListAsync(cancellationToken);

        if (latest.Count == 0) { return Array.Empty<AdminCurrentlyInsideRow>(); }

        var gateIds = latest.Select(x => x.Last.GateId).Distinct().ToList();
        var gateCodes = await appDbContext.Gates.AsNoTracking()
            .Where(g => gateIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        // Split cross-context join into App-then-Identity round-trips.
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
        var profileUserDisplayNames = await userDirectory.GetDisplayNamesAsync(
            inProfileUserIds, cancellationToken);
        var profiles = profileRows.ToDictionary(
            row => row.Id,
            row => new
            {
                row.Id,
                DisplayName = profileUserDisplayNames.TryGetValue(row.UserId, out var name)
                    ? name : string.Empty,
                row.NameArabic,
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
                    profile.NameArabic,
                    profile.ProfileTypeId,
                    profile.ProfileType?.Name,
                    profile.ProfileType?.PageColor,
                    x.Last.ScannedAt,
                    x.Last.GateId,
                    gateCodes.TryGetValue(x.Last.GateId, out var code) ? code : string.Empty);
            })
            .OrderByDescending(row => row.LastCheckInAt)
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
        sheet.Cell(1, 2).Value = "Scanned at";
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
            sheet.Cell(rIdx, 2).Value = row.ScannedAt.ToString("u",
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

    /// <summary>Validates the optional hall-door binding. Null is a no-op
    /// (perimeter gate); a non-null HallId must reference an existing active Hall
    /// in the App DB (logical-FK validation before write, mirroring
    /// <see cref="ValidateProfileTypesAsync"/>), else a clean 400 GATE_HALL_INVALID
    /// instead of a later FK violation.</summary>
    private async Task ValidateHallAsync(
        Guid? hallId, CancellationToken cancellationToken)
    {
        if (hallId is not { } id) { return; }
        var exists = await appDbContext.Halls.AsNoTracking()
            .AnyAsync(hall => hall.Id == id && hall.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(ErrorCodes.GateHallInvalid, 400,
                "The selected hall was not found or is inactive.",
                "القاعة المحددة غير موجودة أو غير نشطة.");
        }
    }

    /// <summary>An assigned operator must actually be able to work
    /// the gate; existence in <c>SIMF_Identity.Users</c> is not enough. Eligible is
    /// either an approved APP account whose profile type is operational
    /// (<c>IsForVisitor=false</c>) and carries a MobileAppRole that confers
    /// <c>Gates.Operate</c> — the owner's app-first model — or an approved
    /// Control-Panel admin account, which is what the retained CP operator console
    /// at <c>/admin/gates/operator</c> signs in as. Anything else (unknown id,
    /// plain visitor, non-operational partner type, deactivated / pending /
    /// rejected account) is rejected with the offending ids named.
    /// Two reads across the DB split, merged in memory — never a cross-database
    /// JOIN.</summary>
    private async Task ValidateOperatorsAsync(
        IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) { return; }
        var distinct = ids.Distinct().ToList();

        var accounts = await identityDbContext.Users.AsNoTracking()
            .Where(user => distinct.Contains(user.Id))
            .Select(user => new { user.Id, user.UserType, user.AccountState })
            .ToListAsync(cancellationToken);

        var approved = accounts
            .Where(account => account.AccountState == AccountState.Approved)
            .ToList();

        // CP-console operators — an approved admin account signing in to
        // /admin/gates/operator (its own Gates.Operate policy is the authority there).
        var eligible = approved
            .Where(account => account.UserType == UserType.Admin)
            .Select(account => account.Id)
            .ToHashSet();

        var appAccountIds = approved
            .Where(account => account.UserType == UserType.Visitor)
            .Select(account => account.Id)
            .ToList();
        if (appAccountIds.Count > 0)
        {
            var operational = await appDbContext.UserProfiles.AsNoTracking()
                .Where(profile => appAccountIds.Contains(profile.UserId)
                    && profile.ProfileType != null
                    && profile.ProfileType.IsActive
                    && !profile.ProfileType.IsForVisitor
                    && GateOperatorAppRoles.Contains(profile.ProfileType.MobileAppRole))
                .Select(profile => profile.UserId)
                .ToListAsync(cancellationToken);
            eligible.UnionWith(operational);
        }

        var rejected = distinct.Where(id => !eligible.Contains(id)).ToList();
        if (rejected.Count > 0)
        {
            var named = string.Join(", ", rejected);
            throw new ApiException(ErrorCodes.GateAssignmentInvalid, 400,
                $"These accounts cannot be assigned as gate operators: {named}. "
                + "A gate operator must be an approved app account whose profile type is "
                + "operational (non-visitor) and carries the Staff or Moderator app role, "
                + "or an approved Control Panel admin account.",
                $"لا يمكن تعيين هذه الحسابات كمشغّلي بوابة: {named}. "
                + "يجب أن يكون مشغّل البوابة حساب تطبيق معتمداً بنوع ملف تشغيلي (غير زائر) "
                + "يحمل دور الموظف أو المشرف، أو حساب مسؤول معتمد في لوحة التحكم.");
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
        Gate gate, IReadOnlyList<Guid> next, Guid actorUserId, DateTime now)
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
            gate.CreatedAt, gate.UpdatedAt, gate.HallId);
}
