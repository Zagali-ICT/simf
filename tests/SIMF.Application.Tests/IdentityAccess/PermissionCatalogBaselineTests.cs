// D-752 — freezes the baseline permission grants of the two CP team roles added
// with the "four permission sets" work (Admin / Security team / PR team /
// Scientific team). These assert the EXACT code set each new role receives from
// PermissionCatalog.All, so a future edit that widens or narrows a team's reach
// (or drops a role from AppRoles.CpRoles) fails the build instead of silently
// changing who can reach the gate / session surfaces.
using SIMF.Common;
using Xunit;

namespace SIMF.Application.Tests.IdentityAccess;

public sealed class PermissionCatalogBaselineTests
{
    // The codes each team role holds as a seeded baseline grant.
    private static HashSet<string> BaselineCodesFor(string role) =>
        PermissionCatalog.All
            .Where(permission => permission.BaselineRoles.Contains(role))
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Both_new_team_roles_are_registered_cp_roles()
    {
        Assert.Contains(AppRoles.SecurityTeam, AppRoles.CpRoles);
        Assert.Contains(AppRoles.ScientificCommittee, AppRoles.CpRoles);
    }

    [Fact]
    public void Security_team_holds_exactly_the_access_control_surface()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            // Gate management + operation + reporting (Operate / ViewOwnReports
            // are shared with the existing GateOperator).
            PermissionCatalog.Gates.Manage,
            PermissionCatalog.Gates.Operate,
            PermissionCatalog.Gates.ViewOwnReports,
            PermissionCatalog.Gates.Export,
            PermissionCatalog.Gates.Import,
            // Hall-door arrival console.
            PermissionCatalog.HallArrivals.View,
            PermissionCatalog.HallArrivals.Record,
            // Session-attendance dashboard.
            PermissionCatalog.Attendance.View,
        };

        Assert.Equal(expected, BaselineCodesFor(AppRoles.SecurityTeam));
    }

    [Fact]
    public void Scientific_team_holds_exactly_the_programme_surface()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            // Sessions lifecycle.
            PermissionCatalog.Sessions.View,
            PermissionCatalog.Sessions.Create,
            PermissionCatalog.Sessions.Edit,
            PermissionCatalog.Sessions.Delete,
            PermissionCatalog.Sessions.Publish,
            PermissionCatalog.Sessions.Export,
            PermissionCatalog.Sessions.Import,
            // Programme days.
            PermissionCatalog.ProgrammeDays.View,
            PermissionCatalog.ProgrammeDays.Create,
            PermissionCatalog.ProgrammeDays.Edit,
            PermissionCatalog.ProgrammeDays.Delete,
            PermissionCatalog.ProgrammeDays.Export,
            PermissionCatalog.ProgrammeDays.Import,
            // Speakers.
            PermissionCatalog.Speakers.View,
            PermissionCatalog.Speakers.Create,
            PermissionCatalog.Speakers.Edit,
            PermissionCatalog.Speakers.Delete,
            PermissionCatalog.Speakers.Export,
            PermissionCatalog.Speakers.Import,
            // Q&A / moderation.
            PermissionCatalog.SessionModeration.Moderate,
            PermissionCatalog.Questions.View,
            PermissionCatalog.Questions.Moderate,
            PermissionCatalog.Questions.Escalate,
            PermissionCatalog.Questions.Export,
            // AI محضر / session summaries.
            PermissionCatalog.SessionSummaries.View,
            PermissionCatalog.SessionSummaries.Edit,
            PermissionCatalog.SessionSummaries.Publish,
            PermissionCatalog.SessionSummaries.Approve,
            PermissionCatalog.SessionSummaries.Export,
            // Ratings / feedback.
            PermissionCatalog.Ratings.View,
            PermissionCatalog.Ratings.Export,
        };

        Assert.Equal(expected, BaselineCodesFor(AppRoles.ScientificCommittee));
    }

    [Fact]
    public void The_two_teams_do_not_overlap()
    {
        var security = BaselineCodesFor(AppRoles.SecurityTeam);
        var scientific = BaselineCodesFor(AppRoles.ScientificCommittee);

        Assert.Empty(security.Intersect(scientific));
    }

    [Fact]
    public void GateOperator_keeps_its_grants_alongside_the_security_team()
    {
        // The shared gate codes must still reach the GateOperator — the Security
        // team was added to those baselines, it did not replace the operator.
        var gateOperator = BaselineCodesFor(AppRoles.GateOperator);

        Assert.Contains(PermissionCatalog.Gates.Operate, gateOperator);
        Assert.Contains(PermissionCatalog.Gates.ViewOwnReports, gateOperator);
    }

    [Fact]
    public void Administrator_is_never_a_baseline_role_it_stays_the_wildcard()
    {
        // Administrator resolves to the wildcard "*" at token-mint time and is
        // therefore never expanded into per-code baseline grants (PermissionResolver).
        var administratorGrants = PermissionCatalog.All
            .Where(permission => permission.BaselineRoles.Contains(AppRoles.Administrator))
            .ToList();

        Assert.Empty(administratorGrants);
    }
}
