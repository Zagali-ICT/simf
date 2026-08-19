// Tests: SIMF.Api.Tests/AdminGatesTests.cs
//        SIMF.Api.Tests/GateOperatorModelTests.cs (operator candidates,
//        operator eligibility validation, gate-form lookups, assignment email)
//        SIMF.Api.Tests/AdminGateCurrentlyInsideTests.cs (occupancy report)
//        SIMF.Api.Tests/GatesExcelTests.cs (gate grid export / import)
using System.Globalization;
using System.Linq.Expressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.AccessControl.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.AccessControl;
using SIMF.Infrastructure.Common.Grids;
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

    /// <summary>The row ceiling on the scan-report Excel export. The export runs the
    /// same column declaration as the grid but with no Skip/Take, so it needs its own
    /// bound; the bespoke filter it replaces let a caller ask for 100,000 scan rows
    /// and build the whole workbook in memory in one request. 10,000 was that
    /// filter's own default, so the export callers actually asked for is
    /// unchanged.</summary>
    private const int ScanExportRowCap = 10_000;

    /// <summary>
    /// The grid contract for /admin/gates: one entry per key GatesList.razor can
    /// send, as both its filter and its sort. A key not declared here is a 400, not
    /// a silently ignored request. isActive is declared because the list has always
    /// honoured an isActive filter, even though the page renders that column
    /// without a filter box.
    /// </summary>
    private static readonly GridColumns<Gate> Columns = new GridColumns<Gate>()
        .Add("code", gate => gate.Code, searchable: true)
        .Add("name", gate => gate.Name, searchable: true)
        .Add("nameArabic", gate => gate.NameArabic, searchable: true)
        .Add("directionMode", gate => gate.DirectionMode)
        .Add("isActive", gate => gate.IsActive)
        .DefaultOrder("code")
        .PageSize(fallback: 25, max: 200);

    /// <summary>The two counts are correlated sub-queries, not declared columns:
    /// the grid renders them but offers no sort or filter on either, so putting
    /// them here keeps them one SELECT and out of the ORDER BY.</summary>
    private static readonly Expression<Func<Gate, AdminGateSummary>> ToSummary =
        gate => new AdminGateSummary(
            gate.Id, gate.Code, gate.Name, gate.NameArabic,
            gate.DirectionMode,
            gate.AllowedProfileTypes.Count,
            gate.Assignments.Count(assignment => assignment.IsActive),
            gate.IsActive, gate.CreatedAt,
            // Carried so the grid Excel export round-trips the
            // bilingual description (positional order matches the record).
            gate.Description, gate.DescriptionArabic);

    /// <summary>
    /// The grid contract for the scan report. It replaces a bespoke filter object
    /// whose four fields were applied with no validation at all: an unknown field was
    /// ignored, an unparseable one never arrived, and the sort was hard-coded.
    ///
    /// <para>
    /// The old FromUtc / ToUtc pair survives as the two hand-written range keys rather
    /// than as the inferred <c>scannedAt</c> filter, because an inferred DateTime
    /// filter names ONE calendar day and a scan report is read over a window. Both
    /// ends are Saudi wall-clock days (<c>GridFilters.ParseDay</c>), and
    /// <c>scannedTo</c> is half-open on the following midnight so the day it names is
    /// included whole — the old <c>&lt;= ToUtc</c> silently excluded everything after
    /// midnight on the last day whenever a caller passed a bare date.
    /// </para>
    ///
    /// <para>
    /// The two searchable columns are the scan's own snapshot fields, not the live
    /// names the rows render: the log is append-only and those are the only
    /// person-identifying values that live on the scanned row itself, so a search can
    /// be a server-side WHERE instead of a second-database round trip per page.
    /// </para>
    /// </summary>
    private static readonly GridColumns<GateScan> ScanColumns = new GridColumns<GateScan>()
        .Add("gateId", scan => scan.GateId)
        .Add("userProfileId", scan => scan.UserProfileId)
        .Add("direction", scan => scan.Direction)
        .Add("outcome", scan => scan.Outcome)
        .Add("denialReasonCode", scan => scan.DenialReasonCode)
        .Add("source", scan => scan.Source)
        .Add("scannedAt", scan => scan.ScannedAt)
        .Add("qrIdAtScan", scan => scan.QrIdAtScan, searchable: true)
        .Add("scannedDisplayName", scan => scan.ScannedDisplayName, searchable: true)
        .AddFilter("scannedFrom", raw => ScannedOnOrAfter(GridFilters.ParseDay("scannedFrom", raw)))
        .AddFilter("scannedTo", raw => ScannedBefore(GridFilters.ParseDay("scannedTo", raw).AddDays(1)))
        .DefaultOrder("scannedAt", descending: true)
        .PageSize(fallback: 50, max: 200);

    // The two range predicates behind scannedFrom / scannedTo. Each closes over a
    // method parameter, which is the shape EF Core turns into a SQL parameter rather
    // than inlining the date as a literal and taking a plan-cache slot per day asked
    // for.
    private static Expression<Func<GateScan, bool>> ScannedOnOrAfter(DateTime from) =>
        scan => scan.ScannedAt >= from;

    private static Expression<Func<GateScan, bool>> ScannedBefore(DateTime exclusiveTo) =>
        scan => scan.ScannedAt < exclusiveTo;

    /// <summary>
    /// The grid contract for the occupancy report. The columns are declared over
    /// <see cref="GateScan"/> because that is what the query pages: the report is one
    /// row per visitor's latest allowed check-in, and everything the CP renders beyond
    /// the gate and the timestamp — the display name, the Arabic name, the profile
    /// type — is resolved AFTER the page, some of it out of the other database. So the
    /// sortable and filterable surface is deliberately the scan's own columns, and a
    /// key naming a resolved field is a 400 rather than a sort that quietly does
    /// nothing.
    /// </summary>
    private static readonly GridColumns<GateScan> CurrentlyInsideColumns =
        new GridColumns<GateScan>()
            .Add("gateId", scan => scan.GateId)
            .Add("scannedAt", scan => scan.ScannedAt)
            .DefaultOrder("scannedAt", descending: true)
            .PageSize(fallback: 25, max: 200);

    public Task<GridPage<AdminGateSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        appDbContext.Gates.ToGridPageAsync(
            query, Columns, gate => gate.Id, ToSummary, cancellationToken);

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
                CreatedBy = actorUserId,
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
                a.CreatedAt, a.CreatedBy,
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

        // Step 2 (App DB) — the profile rows carrying one of those types. An
        // attendee with no account is skipped: a gate operator works the gate by
        // signing in to the mobile app, so there is nothing for step 3 to approve.
        var profileTypeIds = operatorProfileTypes.Select(row => row.Id).ToList();
        var profileRows = await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId != null
                && profile.ProfileTypeId != null
                && profileTypeIds.Contains(profile.ProfileTypeId.Value))
            .Select(profile => new
            {
                UserId = profile.UserId!.Value,
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

    public async Task<GridPage<AdminGateScanRow>> ListScansAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var scans = appDbContext.GateScans.AsNoTracking()
            .ApplyGrid(query, ScanColumns, scan => scan.Id);

        var (skip, top) = query.ClampPage(ScanColumns.FallbackTop, ScanColumns.MaxTop);
        var total = await scans.CountAsync(cancellationToken);

        var rows = await BuildScanRowsAsync(
            scans.Skip(skip).Take(top), cancellationToken);

        return GridPage<AdminGateScanRow>.Of(rows, total, skip, top);
    }

    /// <summary>Turns an already-filtered, already-ordered, already-bounded scan
    /// query into report rows. Shared by the grid and the Excel export so the two
    /// cannot render the same scan differently; each caller decides its own bound
    /// first (a page window, or <see cref="ScanExportRowCap"/>), because the join may
    /// not run over an unbounded set.</summary>
    private async Task<IReadOnlyList<AdminGateScanRow>> BuildScanRowsAsync(
        IQueryable<GateScan> scans, CancellationToken cancellationToken)
    {
        // The visitor name is the scan row's OWN immutable snapshot, written at scan
        // time. It used to be resolved profile-then-Identity out of
        // SimfUser.DisplayName, which resolves to nothing for a holder with no account
        // — the ordinary badge holder — so the Visitor column and its Excel export
        // went blank for exactly the people the walk-in desk mints badges for, while
        // the correct name sat unread on the row. Reading it here also drops two
        // cross-database round trips per page and lets the whole row be a projection
        // rather than a materialised nineteen-column entity.
        return await scans
            .Join(appDbContext.Gates.AsNoTracking(),
                scan => scan.GateId, gate => gate.Id,
                (scan, gate) => new AdminGateScanRow(
                    scan.Id, scan.GateId, gate.Code,
                    scan.UserProfileId, scan.ScannedDisplayName,
                    scan.QrIdAtScan, scan.Direction, scan.Outcome,
                    scan.DenialReasonCode, scan.ScannedAt,
                    scan.ScannedByUserId, scan.Source))
            .ToListAsync(cancellationToken);
    }

    public async Task<GridPage<AdminCurrentlyInsideRow>> ListCurrentlyInsideAsync(
        GridQuery query, CancellationToken cancellationToken = default)
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
        //
        // "Latest allowed check-in still inside the window" is the report's SCOPE, so
        // it is composed onto the source BEFORE the grid: the grid's own filters,
        // ordering and page window then apply to the occupancy set rather than to the
        // raw scan log. The report used to page nothing at all and sort in C# after
        // materialising every matching GateScan entity — 19 properties per row off the
        // highest-write table in the system, to render three of them.
        var insideScans = appDbContext.GateScans.AsNoTracking()
            .Where(s => s.Outcome == ScanOutcome.Allowed
                && s.UserProfileId != null
                && s.Direction == ScanDirection.CheckIn
                && s.ScannedAt >= presenceCutoff
                && !appDbContext.GateScans.Any(later =>
                    later.UserProfileId == s.UserProfileId
                    && later.Outcome == ScanOutcome.Allowed
                    && (later.ScannedAt > s.ScannedAt
                        || (later.ScannedAt == s.ScannedAt && later.Id > s.Id))))
            .ApplyGrid(query, CurrentlyInsideColumns, scan => scan.Id);

        var (skip, top) = query.ClampPage(
            CurrentlyInsideColumns.FallbackTop, CurrentlyInsideColumns.MaxTop);

        // The stat card on the dashboard reads Total, so occupancy stays the true
        // count of everyone inside even when the page shows the first 25 of them.
        var total = await insideScans.CountAsync(cancellationToken);

        var latest = await insideScans
            .Skip(skip).Take(top)
            .Select(s => new
            {
                UserProfileId = s.UserProfileId!.Value,
                s.GateId,
                s.ScannedAt,
            })
            .ToListAsync(cancellationToken);

        if (latest.Count == 0)
        {
            return GridPage<AdminCurrentlyInsideRow>.Of(
                Array.Empty<AdminCurrentlyInsideRow>(), total, skip, top);
        }

        var gateIds = latest.Select(scan => scan.GateId).Distinct().ToList();
        var gateCodes = await appDbContext.Gates.AsNoTracking()
            .Where(gate => gateIds.Contains(gate.Id))
            .Select(gate => new { gate.Id, gate.Code })
            .ToDictionaryAsync(gate => gate.Id, gate => gate.Code, cancellationToken);

        // Split cross-context join into App-then-Identity round-trips.
        // The Include is gone, not lost: it is inert under the projection below, which
        // pulls the profile-type fields the row needs without materialising the entity.
        var profileIds = latest.Select(scan => scan.UserProfileId).Distinct().ToList();
        var profileRows = await appDbContext.UserProfiles.AsNoTracking()
            .Where(profile => profileIds.Contains(profile.Id))
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.Name,
                profile.NameArabic,
                profile.ProfileTypeId,
                profile.ProfileType,
            })
            .ToListAsync(cancellationToken);
        // Only the accounts are looked up, but EVERY profile stays in the map: an
        // attendee with no account has no display name to fetch, and dropping them
        // would take them off the occupancy roster while they are still inside. Their
        // Arabic name and profile type come from the profile row either way — and so
        // does the English one when there is no account, which is the ordinary badge
        // holder. Falling straight through to empty listed them nameless; the profile
        // name is what the badge is printed from, which is the same order of
        // preference the QR resolver applies at the door.
        var profileAccountUserIds = profileRows
            .Where(profileRow => profileRow.UserId != null)
            .Select(profileRow => profileRow.UserId!.Value)
            .Distinct()
            .ToList();
        var profileUserDisplayNames = await userDirectory.GetDisplayNamesAsync(
            profileAccountUserIds, cancellationToken);
        var profiles = profileRows.ToDictionary(row => row.Id);

        var items = latest
            .Where(scan => profiles.ContainsKey(scan.UserProfileId))
            .Select(scan =>
            {
                var profile = profiles[scan.UserProfileId];
                return new AdminCurrentlyInsideRow(
                    scan.UserProfileId,
                    profile.UserId is { } userId
                        && profileUserDisplayNames.TryGetValue(userId, out var name)
                            ? name : profile.Name,
                    profile.NameArabic,
                    profile.ProfileTypeId,
                    profile.ProfileType?.Name,
                    profile.ProfileType?.PageColor,
                    scan.ScannedAt,
                    scan.GateId,
                    gateCodes.TryGetValue(scan.GateId, out var code) ? code : string.Empty);
            })
            .ToList();

        // No C#-side re-sort: the order is the SQL ORDER BY the grid composed, and
        // re-sorting a page in memory would only ever contradict it.
        return GridPage<AdminCurrentlyInsideRow>.Of(items, total, skip, top);
    }

    public async Task<byte[]> ExportScansXlsxAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // Same declaration, same filters, same order as the grid — and deliberately no
        // Skip/Take, which is the whole reason composition and paging are separate
        // calls. The export's bound is its own row cap, not the grid's page size.
        var scans = appDbContext.GateScans.AsNoTracking()
            .ApplyGrid(query, ScanColumns, scan => scan.Id)
            .Take(ScanExportRowCap);
        var rows = await BuildScanRowsAsync(scans, cancellationToken);

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
            // Row 1 is the header, so the first data row is 2.
            var excelRow = rowIndex + 2;
            sheet.Cell(excelRow, 1).Value = row.ScanId;
            sheet.Cell(excelRow, 2).Value = row.ScannedAt.ToString("u",
                CultureInfo.InvariantCulture);
            sheet.Cell(excelRow, 3).Value = row.GateCode;
            sheet.Cell(excelRow, 4).Value = row.VisitorDisplayName ?? string.Empty;
            sheet.Cell(excelRow, 5).Value = row.QrIdAtScan;
            sheet.Cell(excelRow, 6).Value = row.Direction.ToString();
            sheet.Cell(excelRow, 7).Value = row.Outcome.ToString();
            sheet.Cell(excelRow, 8).Value = row.DenialReasonCode?.ToString() ?? string.Empty;
            sheet.Cell(excelRow, 9).Value = row.Source.ToString();
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
                .Where(profile => profile.UserId != null
                    && appAccountIds.Contains(profile.UserId.Value)
                    && profile.ProfileType != null
                    && profile.ProfileType.IsActive
                    && !profile.ProfileType.IsForVisitor
                    && GateOperatorAppRoles.Contains(profile.ProfileType.MobileAppRole))
                .Select(profile => profile.UserId!.Value)
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
                    CreatedBy = actorUserId,
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
