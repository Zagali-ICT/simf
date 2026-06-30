// Issue-1 — guards the permission catalogue's invariants: codes are unique,
// the policy-name helpers round-trip, and the six pre-catalogue codes keep
// their exact strings (the wire/seed contract).
using SIMF.Common;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Application.Tests.IdentityAccess;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Every_code_is_unique()
    {
        var codes = PermissionCatalog.All.Select(permission => permission.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_entry_has_a_page_action_and_display_name()
    {
        Assert.All(PermissionCatalog.All, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Page));
            Assert.False(string.IsNullOrWhiteSpace(permission.Action));
            Assert.False(string.IsNullOrWhiteSpace(permission.DisplayName));
            Assert.Equal($"{permission.Page}.{permission.Action}", permission.Code);
        });
    }

    [Fact]
    public void Policy_name_round_trips()
    {
        var policy = PermissionCatalog.PolicyFor(PermissionCatalog.Sessions.Edit);

        Assert.Equal("perm:Sessions.Edit", policy);
        Assert.True(PermissionCatalog.IsPermissionPolicy(policy));
        Assert.Equal(PermissionCatalog.Sessions.Edit, PermissionCatalog.CodeFromPolicy(policy));
    }

    [Fact]
    public void Non_permission_policy_names_are_not_misclassified()
    {
        Assert.False(PermissionCatalog.IsPermissionPolicy("AdministratorOnly"));
        Assert.False(PermissionCatalog.IsPermissionPolicy("RequireApprovedAccount"));
    }

    [Theory]
    [InlineData("Gates.Manage")]
    [InlineData("Gates.Operate")]
    [InlineData("Gates.ViewOwnReports")]
    [InlineData("Invitations.Manage")]
    [InlineData("Vips.View")]
    [InlineData("Vips.Notify")]
    public void The_six_pre_catalogue_codes_are_present_unchanged(string code)
    {
        Assert.Contains(PermissionCatalog.All, permission => permission.Code == code);
    }

    [Fact]
    public void Baseline_grants_match_the_existing_roles()
    {
        // GateOperator keeps the gate operator pair; PublicRelations keeps the
        // invitation + VIP set (+ News, carried over from the PR-gated endpoints).
        var gateOperator = PermissionCatalog.All
            .Where(p => p.BaselineRoles.Contains(AppRoles.GateOperator))
            .Select(p => p.Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(PermissionCatalog.Gates.Operate, gateOperator);
        Assert.Contains(PermissionCatalog.Gates.ViewOwnReports, gateOperator);
        Assert.DoesNotContain(PermissionCatalog.Gates.Manage, gateOperator);

        var publicRelations = PermissionCatalog.All
            .Where(p => p.BaselineRoles.Contains(AppRoles.PublicRelations))
            .Select(p => p.Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains(PermissionCatalog.Invitations.Manage, publicRelations);
        Assert.Contains(PermissionCatalog.Vips.Notify, publicRelations);
    }

    // ── D-563: operational permissions derived from the ProfileType's app role ──

    [Fact]
    public void Staff_app_role_grants_gate_and_walk_in_permissions()
    {
        var staff = PermissionCatalog.OperationalPermissionsForAppRole(MobileAppRole.Staff);

        // The two staff app screens: gate scanner + walk-in registration desk.
        Assert.Contains(PermissionCatalog.Gates.Operate, staff);
        Assert.Contains(PermissionCatalog.Gates.ViewOwnReports, staff);
        Assert.Contains(PermissionCatalog.Visitors.RegisterOnsite, staff);
        // Least-privilege: no app screen registers partner "Other" accounts, so
        // Others.RegisterOnsite is deliberately NOT granted (CP-desk-only).
        Assert.DoesNotContain(PermissionCatalog.Others.RegisterOnsite, staff);
        // Staff is operational, not administrative — never the wildcard.
        Assert.DoesNotContain(PermissionCatalog.Wildcard, staff);
    }

    [Fact]
    public void Moderator_app_role_is_a_superset_of_staff_plus_moderation()
    {
        var staff = PermissionCatalog.OperationalPermissionsForAppRole(MobileAppRole.Staff);
        var moderator = PermissionCatalog.OperationalPermissionsForAppRole(MobileAppRole.Moderator);

        Assert.All(staff, code => Assert.Contains(code, moderator));
        Assert.Contains(PermissionCatalog.Questions.Moderate, moderator);
        Assert.Contains(PermissionCatalog.SessionModeration.Moderate, moderator);
        Assert.Contains(PermissionCatalog.Comments.Moderate, moderator);
    }

    [Theory]
    [InlineData(MobileAppRole.None)]
    [InlineData(MobileAppRole.Visitor)]
    [InlineData(MobileAppRole.Exhibitor)]
    public void Non_operational_app_roles_grant_no_permissions(MobileAppRole role)
    {
        Assert.Empty(PermissionCatalog.OperationalPermissionsForAppRole(role));
    }

    [Theory]
    [InlineData(MobileAppRole.Staff)]
    [InlineData(MobileAppRole.Moderator)]
    public void App_role_permissions_are_all_real_catalogue_codes(MobileAppRole role)
    {
        var valid = PermissionCatalog.All
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            PermissionCatalog.OperationalPermissionsForAppRole(role),
            code => Assert.Contains(code, valid));
    }
}
