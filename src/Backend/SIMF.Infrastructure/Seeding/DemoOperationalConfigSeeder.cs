// Tests: SIMF.Api.Tests/DemoOperationalConfigSeederTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Domain.AccessControl;
using SIMF.Domain.SeatReservations;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

namespace SIMF.Infrastructure.Seeding;

/// <summary>
/// BUG-023 — seeds the OPERATIONAL configuration the demo accounts need before
/// three whole journeys can be exercised at all. A seeded QA database carried
/// <c>Gates=0</c>, <c>GateAssignments=0</c>, <c>GateProfileTypeAllow=0</c>,
/// <c>SessionModerators=0</c> and <c>HallSeatLayouts=0</c>, so the gate scanner
/// answered "you are not assigned to any gate", the moderation desk answered "you
/// are not a moderator for this session", and
/// <c>GET /app/sessions/{id}/seats</c> returned an empty grid. The guards were all
/// correct — the configuration simply did not exist.
///
/// <para><b>Why C# and not the SQL content lane (D-718).</b> The content lane
/// (<c>docs/migrations/2026/*.sql</c>) runs against <c>SIMF_App</c> only, and every
/// row here binds an App entity to an <b>Identity</b> user id (the demo staff
/// operator, the demo moderator). Resolving those ids in SQL would mean a cross-database
/// join, which D-157 forbids outright. So this stays in C# next to the demo-account
/// seeder that mints those users, and is gated exactly like it: Development, or an
/// explicit <c>Seed:EnableDemoAccounts</c> opt-in. Production is clean by construction.</para>
///
/// <para>It runs AFTER the SQL content seed because it configures the content that
/// seed creates (the MAIN hall and the programme sessions). Everything it writes is
/// keyed on a stable identifier and inserted only when absent, so re-running is a
/// no-op and an admin edit is never overwritten.</para>
/// </summary>
public sealed class DemoOperationalConfigSeeder(
    SimfAppDbContext appDbContext,
    IUserAccountRepository accounts,
    IOptions<DemoSeedOptions> demoOptions,
    TimeProvider timeProvider,
    IHostEnvironment hostEnvironment,
    ILogger<DemoOperationalConfigSeeder> logger)
{
    /// <summary>The demo gate operator — the Staff demo account (D-585), whose
    /// profile type carries <see cref="MobileAppRole.Staff"/>.</summary>
    private const string GateOperatorEmail = "staff@simf.local";

    /// <summary>The demo session moderator — the Moderator demo account (D-585).</summary>
    private const string ModeratorEmail = "moderator@simf.local";

    /// <summary>The hall the 2026 programme seed creates
    /// (<c>docs/migrations/2026/SIMF_App_Programme.sql</c>).</summary>
    private const string MainHallCode = "MAIN";

    /// <summary>The seat grid for the main hall: ten rows of twenty (200 seats),
    /// comfortably inside the seeded hall capacity of 500, so the app's seat picker
    /// has a real grid to draw instead of <c>rowLabels: []</c>.</summary>
    private const string MainHallRowLabels = "A,B,C,D,E,F,G,H,I,J";
    private const int MainHallSeatsPerRow = 20;

    /// <summary>The two demo gates. The venue gate carries NO profile-type allow
    /// rows (a general gate — every badge passes); the VIP gate carries them, so the
    /// allow-list branch of the constraint engine is exercised too.</summary>
    private static readonly (string Code, string Name, string NameArabic,
        DirectionMode DirectionMode, string[] AllowedProfileTypes)[] DemoGates =
    [
        ("MAIN-GATE", "Main Venue Gate", "البوابة الرئيسية", DirectionMode.Both, []),
        ("VIP-GATE", "VIP Gate", "بوابة كبار الشخصيات", DirectionMode.Both, ["VVIP", "VIP"]),
    ];

    /// <summary>The programme sessions whose live Q&amp;A the demo moderator runs —
    /// every "Q&amp;A Session" and "Panel" across the three days
    /// (<c>SIMF_App_Programme.sql</c> session codes).</summary>
    private static readonly string[] ModeratedSessionCodes =
    [
        "D1-16", "D1-17", "D1-24", "D1-25",
        "D2-06", "D2-07", "D2-14", "D2-15",
        "D3-06", "D3-07",
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Same gate as the demo accounts themselves (IdentitySeeder): this
        // configuration only makes sense alongside them, and it must never exist in
        // production.
        if (!hostEnvironment.IsDevelopment() && !demoOptions.Value.EnableDemoAccounts)
        {
            logger.LogInformation(
                "Demo operational-config seed skipped — not Development and "
                + "Seed:EnableDemoAccounts is false.");
            return;
        }

        await EnsureGatesAsync(cancellationToken);
        await EnsureSessionModeratorsAsync(cancellationToken);
        await EnsureMainHallSeatLayoutAsync(cancellationToken);
    }

    /// <summary>Seeds <see cref="DemoGates"/> with the demo staff account assigned as
    /// their operator, so the app's gate scanner finds an assignment instead of
    /// refusing with "you are not assigned to any gate". Idempotent by gate
    /// <c>Code</c>; the assignment and the allow rows are added only to a gate this
    /// pass creates, so an admin who later revokes either is not overridden.</summary>
    private async Task EnsureGatesAsync(CancellationToken cancellationToken)
    {
        var operatorUser = await accounts.FindByEmailAsync(GateOperatorEmail, cancellationToken);
        if (operatorUser is null)
        {
            logger.LogInformation(
                "Demo gate seed skipped — {Email} does not exist.", GateOperatorEmail);
            return;
        }

        var wantedCodes = DemoGates.Select(gate => gate.Code).ToList();
        var existingCodes = (await appDbContext.Gates
                .Where(gate => wantedCodes.Contains(gate.Code))
                .Select(gate => gate.Code)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profileTypeIdsByName = await appDbContext.ProfileTypes
            .Where(profileType => profileType.IsActive)
            .ToDictionaryAsync(
                profileType => profileType.Name, profileType => profileType.Id, cancellationToken);

        var now = timeProvider.SimfNow();
        var added = 0;
        foreach (var demo in DemoGates)
        {
            if (existingCodes.Contains(demo.Code)) { continue; }

            var gate = new Gate
            {
                Id = Guid.NewGuid(),
                Code = demo.Code,
                Name = demo.Name,
                NameArabic = demo.NameArabic,
                DirectionMode = demo.DirectionMode,
                IsActive = true,
                CreatedAt = now,
            };
            foreach (var profileTypeName in demo.AllowedProfileTypes)
            {
                if (!profileTypeIdsByName.TryGetValue(profileTypeName, out var profileTypeId))
                {
                    continue;
                }
                gate.AllowedProfileTypes.Add(new GateProfileTypeAllow
                {
                    GateId = gate.Id,
                    ProfileTypeId = profileTypeId,
                });
            }
            gate.Assignments.Add(new GateAssignment
            {
                Id = Guid.NewGuid(),
                GateId = gate.Id,
                UserId = operatorUser.Id,
                IsActive = true,
                CreatedAt = now,
                CreateBy = operatorUser.Id,
            });
            appDbContext.Gates.Add(gate);
            added++;
        }

        if (added > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Demo gates ensured (created {Count} of {Total}).", added, DemoGates.Length);
    }

    /// <summary>Grants the demo moderator the per-session moderation right on the
    /// programme's Q&amp;A and panel sessions, so the moderation desk opens instead
    /// of refusing with "you are not a moderator for this session". Idempotent by
    /// the (SessionId, UserId) composite key.</summary>
    private async Task EnsureSessionModeratorsAsync(CancellationToken cancellationToken)
    {
        var moderator = await accounts.FindByEmailAsync(ModeratorEmail, cancellationToken);
        if (moderator is null)
        {
            logger.LogInformation(
                "Demo session-moderator seed skipped — {Email} does not exist.", ModeratorEmail);
            return;
        }

        var sessionIds = await appDbContext.Sessions
            .Where(session => session.IsActive && ModeratedSessionCodes.Contains(session.Code))
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);
        if (sessionIds.Count == 0)
        {
            logger.LogInformation(
                "Demo session-moderator seed skipped — none of the programme sessions exist yet.");
            return;
        }

        var alreadyGranted = (await appDbContext.SessionModerators
                .Where(grant => grant.UserId == moderator.Id && sessionIds.Contains(grant.SessionId))
                .Select(grant => grant.SessionId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var now = timeProvider.SimfNow();
        var added = 0;
        foreach (var sessionId in sessionIds)
        {
            if (alreadyGranted.Contains(sessionId)) { continue; }
            appDbContext.SessionModerators.Add(new SessionModerator
            {
                SessionId = sessionId,
                UserId = moderator.Id,
                AssignedByUserId = moderator.Id,
                AssignedAt = now,
            });
            added++;
        }

        if (added > 0)
        {
            await appDbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation(
            "Demo session moderators ensured (granted {Count} of {Total} session(s)).",
            added, sessionIds.Count);
    }

    /// <summary>Gives the seeded main hall a seat grid, so
    /// <c>GET /app/sessions/{id}/seats</c> returns real row labels instead of
    /// <c>rowLabels: []</c> / <c>seatsPerRow: 0</c> and the app's seat picker has
    /// something to draw. Idempotent: a hall that already has a layout (seeded or
    /// admin-authored) is left alone.</summary>
    private async Task EnsureMainHallSeatLayoutAsync(CancellationToken cancellationToken)
    {
        var hall = await appDbContext.Halls
            .Where(row => row.Code == MainHallCode)
            .Select(row => new { row.Id, row.Capacity })
            .SingleOrDefaultAsync(cancellationToken);
        if (hall is null)
        {
            logger.LogInformation(
                "Demo seat-layout seed skipped — hall '{Code}' does not exist.", MainHallCode);
            return;
        }

        if (await appDbContext.HallSeatLayouts.AnyAsync(
            layout => layout.HallId == hall.Id, cancellationToken))
        {
            return; // already laid out — never overwrite.
        }

        var rowCount = MainHallRowLabels.Split(',').Length;
        var layoutCapacity = rowCount * MainHallSeatsPerRow;
        if (layoutCapacity > hall.Capacity)
        {
            // The same rule SeatReservationService.SetLayoutAsync enforces; a hall
            // an admin shrank below the demo grid keeps no layout rather than an
            // invalid one.
            logger.LogWarning(
                "Demo seat-layout seed skipped — grid capacity {LayoutCapacity} exceeds "
                + "hall '{Code}' capacity {HallCapacity}.",
                layoutCapacity, MainHallCode, hall.Capacity);
            return;
        }

        appDbContext.HallSeatLayouts.Add(new HallSeatLayout
        {
            Id = Guid.NewGuid(),
            HallId = hall.Id,
            RowLabels = MainHallRowLabels,
            SeatsPerRow = MainHallSeatsPerRow,
            CreatedAt = timeProvider.SimfNow(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Demo seat layout seeded for hall '{Code}' ({Rows} rows x {Seats} seats).",
            MainHallCode, rowCount, MainHallSeatsPerRow);
    }
}
