// P1.3 (D-214) — PUT /api/v1/admin/visitors/{id} and /api/v1/admin/others/{id}.
// Per-user edit: email + display name + tier; an email change rolls the
// security stamp + revokes sessions (verified via stamp change).
//
// Also covers the two Bi-Meeting eligibility flags on the visitor edit. They used
// to be non-nullable booleans on the request, so a partial PUT that omitted them
// deserialised both as false and silently withdrew the eligibility. They are now
// bool? with "omitted = unchanged" semantics, and an explicit false must still
// clear the flag — revoking it has to stay possible.
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
using SIMF.Domain.Profiles;
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

    // A partial edit — one that never mentions the meeting flags — must leave both
    // of them exactly as the admin last set them. The body is an anonymous object
    // rather than the typed request because the defect is about fields being ABSENT
    // from the JSON, which is what an API client sends and what the typed request
    // cannot express.
    [Fact]
    public async Task Update_visitor_omitting_the_meeting_flags_leaves_them_unchanged()
    {
        var token = await CreateAdminAndSignInAsync();
        var visitor = await CreateVisitorWithMeetingFlagsAsync(
            allowsSpeakerMeeting: true, allowsDelegationMeeting: true);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{visitor.Id}",
            new
            {
                Email = visitor.Email,
                DisplayName = "Partial Edit",
                // Resent so the tier is not cleared: ProfileTypeId keeps its
                // "null means clear the tier" meaning, which is why only the two
                // booleans changed shape.
                ProfileTypeId = visitor.ProfileTypeId,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await LoadProfileAsync(visitor.Id);
        Assert.True(profile.AllowsSpeakerMeeting);
        Assert.True(profile.AllowsDelegationMeeting);
    }

    // The other half of the same rule: making "omitted" mean unchanged must not
    // make false unreachable, or an admin could never revoke an eligibility.
    [Fact]
    public async Task Update_visitor_setting_the_meeting_flags_false_clears_them()
    {
        var token = await CreateAdminAndSignInAsync();
        var visitor = await CreateVisitorWithMeetingFlagsAsync(
            allowsSpeakerMeeting: true, allowsDelegationMeeting: true);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{visitor.Id}",
            new AdminUpdateVisitorRequest
            {
                Email = visitor.Email,
                DisplayName = "Revoked",
                ProfileTypeId = visitor.ProfileTypeId,
                AllowsSpeakerMeeting = false,
                AllowsDelegationMeeting = false,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await LoadProfileAsync(visitor.Id);
        Assert.False(profile.AllowsSpeakerMeeting);
        Assert.False(profile.AllowsDelegationMeeting);
    }

    // One flag may be revoked while the other is left alone, which is the case a
    // single shared "were any flags sent?" check would have got wrong.
    [Fact]
    public async Task Update_visitor_can_revoke_one_meeting_flag_and_omit_the_other()
    {
        var token = await CreateAdminAndSignInAsync();
        var visitor = await CreateVisitorWithMeetingFlagsAsync(
            allowsSpeakerMeeting: true, allowsDelegationMeeting: true);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{visitor.Id}",
            new
            {
                Email = visitor.Email,
                DisplayName = "Half Revoked",
                ProfileTypeId = visitor.ProfileTypeId,
                AllowsSpeakerMeeting = false,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await LoadProfileAsync(visitor.Id);
        Assert.False(profile.AllowsSpeakerMeeting);
        Assert.True(profile.AllowsDelegationMeeting);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<UserProfile> LoadProfileAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);
    }

    // An approved visitor that already HAS an App-DB profile row carrying the two
    // meeting flags, plus the tier the edit has to resend so the assertions are
    // about the flags alone.
    private async Task<(Guid Id, string Email, Guid ProfileTypeId)>
        CreateVisitorWithMeetingFlagsAsync(
            bool allowsSpeakerMeeting, bool allowsDelegationMeeting)
    {
        var email = $"flags-visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Flags Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = await db.ProfileTypes
            .FirstOrDefaultAsync(p => p.IsForVisitor && p.IsActive);
        if (type is null)
        {
            type = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = "Visitor - FlagSeed",
                NameArabic = "زائر",
                PageColor = "#3B82F6",
                IsForVisitor = true,
                IsActive = true,
                CreatedAt = SimfClock.Now,
            };
            db.ProfileTypes.Add(type);
            await db.SaveChangesAsync();
        }

        db.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = type.Id,
            Name = "Flags Visitor",
            NameArabic = "Flags Visitor",
            AllowsSpeakerMeeting = allowsSpeakerMeeting,
            AllowsDelegationMeeting = allowsDelegationMeeting,
            CreatedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
        return (user.Id, email, type.Id);
    }

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
