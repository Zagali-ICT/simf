// P1.3 (D-214) — PUT /api/v1/admin/visitors/{id} and /api/v1/admin/others/{id}.
// Per-user edit: email + display name + tier; an email change rolls the
// security stamp + revokes sessions (verified via stamp change).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminUpdateUserTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUpdateUserTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Update_visitor_changes_email_and_display_name()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreateVisitorAsync();
        var newEmail = $"edited-{Guid.NewGuid():N}@simf.test";

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = newEmail, DisplayName = "Edited Name" },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.Equal(newEmail, user.Email);
        Assert.Equal("Edited Name", user.DisplayName);
    }

    [Fact]
    public async Task Update_visitor_email_change_rolls_security_stamp()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreateVisitorAsync();

        string stampBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            stampBefore = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).SecurityStamp!;
        }

        await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = $"rolled-{Guid.NewGuid():N}@simf.test",
                DisplayName = "Rolled",
            },
            token);

        using var after = _factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var stampAfter = (await db2.Users.AsNoTracking().SingleAsync(u => u.Id == id)).SecurityStamp!;
        Assert.NotEqual(stampBefore, stampAfter);
    }

    // #24 — an admin correcting a login email (the new-account typo case) marks
    // the corrected address unverified, so the next sign-in re-verifies it via the
    // 2FA email-OTP (sign-in gates on AccountState, not EmailConfirmed).
    [Fact]
    public async Task Update_visitor_email_change_marks_email_unconfirmed()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreateVisitorAsync();

        await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest
            {
                Email = $"corrected-{Guid.NewGuid():N}@simf.test",
                DisplayName = "Corrected",
            },
            token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.False(user.EmailConfirmed);
    }

    // A rename that leaves the email unchanged must NOT drop the confirmed flag.
    [Fact]
    public async Task Update_visitor_without_email_change_keeps_email_confirmed()
    {
        var token = await CreateAdminAndSignInAsync();
        var email = $"stable-{Guid.NewGuid():N}@simf.test";
        var id = await CreateVisitorAsync(email);

        await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = email, DisplayName = "Renamed Only" },
            token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task Update_visitor_duplicate_email_is_409()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreateVisitorAsync();
        var otherEmail = $"taken-{Guid.NewGuid():N}@simf.test";
        await CreateVisitorAsync(otherEmail);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = otherEmail, DisplayName = "Clash" },
            token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_visitor_short_display_name_is_400()
    {
        var token = await CreateAdminAndSignInAsync();
        var id = await CreateVisitorAsync();

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{id}",
            new AdminUpdateVisitorRequest { Email = $"x-{Guid.NewGuid():N}@simf.test", DisplayName = "a" },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_visitor_unknown_id_is_404()
    {
        var token = await CreateAdminAndSignInAsync();
        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}",
            new AdminUpdateVisitorRequest { Email = $"x-{Guid.NewGuid():N}@simf.test", DisplayName = "Ghost" },
            token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_visitor_non_admin_is_forbidden()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{Guid.NewGuid()}",
            new AdminUpdateVisitorRequest { Email = $"x-{Guid.NewGuid():N}@simf.test", DisplayName = "Nope" },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreateVisitorAsync(string? email = null)
    {
        email ??= $"edit-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Edit Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"edit-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Edit Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
