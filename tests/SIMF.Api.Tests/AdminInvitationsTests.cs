// D-168 (gap doc G5, PDF §2.7.3) — public-relations: invitations +
// VIP list + bulk-notify. Mirrors AdminSessionsTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminInvitationsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string PublicRelationsRole = "PublicRelations";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminInvitationsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_returns_invitation_with_recipient_data_projected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profile = await SeedVisitorProfileAsync("vvip");

        var response = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest
            {
                SentToUserProfileId = profile.Id,
                Notes = "Please join the gala dinner.",
            },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;
        Assert.Equal(profile.Id, detail.SentToUserProfileId);
        Assert.Equal(InvitationState.Pending, detail.State);
        Assert.Equal("Please join the gala dinner.", detail.Notes);
        Assert.Equal(profile.Name, detail.RecipientEnglishName);
    }

    [Fact]
    public async Task Create_with_unknown_profile_is_400_INVITATION_TARGET_NOT_FOUND()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest
            {
                SentToUserProfileId = Guid.NewGuid(),
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InvitationTargetNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Update_to_Confirmed_sets_RespondedAt_and_records_state_change()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profile = await SeedVisitorProfileAsync("vip");
        var created = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest { SentToUserProfileId = profile.Id },
            token);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;

        var updated = await PutAuthAsync(
            $"/api/v1/admin/invitations/{detail.Id}",
            new AdminUpdateInvitationRequest
            {
                State = InvitationState.Confirmed,
                Notes = detail.Notes,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = (await updated.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;
        Assert.Equal(InvitationState.Confirmed, after.State);
        Assert.NotNull(after.RespondedAt);
    }

    [Fact]
    public async Task Update_from_Confirmed_back_to_Pending_is_400_INVITATION_STATE_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profile = await SeedVisitorProfileAsync("gold");
        var created = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest { SentToUserProfileId = profile.Id },
            token);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;
        var confirm = await PutAuthAsync(
            $"/api/v1/admin/invitations/{detail.Id}",
            new AdminUpdateInvitationRequest { State = InvitationState.Confirmed },
            token);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var revert = await PutAuthAsync(
            $"/api/v1/admin/invitations/{detail.Id}",
            new AdminUpdateInvitationRequest { State = InvitationState.Pending },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, revert.StatusCode);
        var body = (await revert.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InvitationStateInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Deactivate_marks_invitation_inactive_and_is_idempotent()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var profile = await SeedVisitorProfileAsync("vvip");
        var created = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest { SentToUserProfileId = profile.Id },
            token);
        var detail = (await created.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;

        var first = await DeleteAuthAsync($"/api/v1/admin/invitations/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await DeleteAuthAsync($"/api/v1/admin/invitations/{detail.Id}", token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/invitations/{detail.Id}", token);
        var after = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminInvitationDetail>>())!.Data!;
        Assert.False(after.IsActive);
    }

    [Fact]
    public async Task Vip_list_returns_only_VVIP_VIP_Gold_profiles()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var vipProfile = await SeedVisitorProfileAsync("vip");
        await SeedVisitorProfileAsync("general"); // non-VIP

        var response = await PostAuthAsync(
            "/api/v1/admin/vips/list",
            new GridQuery { Top = 100 },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminVipSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.UserProfileId == vipProfile.Id);
        Assert.All(page.Items, item =>
            Assert.Contains(item.ProfileTypeName,
                new[] { "VVIP", "VIP", "Gold" }));
    }

    [Fact]
    public async Task NotifyVips_dispatches_and_records_skips_for_non_VIP_ids()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var vipProfile = await SeedVisitorProfileAsync("vvip");
        var nonVipProfile = await SeedVisitorProfileAsync("general");

        var response = await PostAuthAsync(
            "/api/v1/admin/vips/notify",
            new AdminNotifyVipsRequest
            {
                UserProfileIds = new List<Guid> { vipProfile.Id, nonVipProfile.Id },
                Title = "Welcome reception",
                TitleArabic = "حفل استقبال",
                Body = "Please join us at 7 PM tonight.",
                BodyArabic = "نأمل حضوركم الساعة 7 مساءً.",
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminNotifyVipsResult>>())!.Data!;
        Assert.Equal(1, result.Dispatched);
        Assert.Contains(nonVipProfile.Id, result.SkippedProfileIds);
    }

    [Fact]
    public async Task NotifyVips_with_empty_ids_is_400_VIP_NOTIFY_EMPTY()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/vips/notify",
            new AdminNotifyVipsRequest
            {
                UserProfileIds = new List<Guid>(),
                Title = "t", TitleArabic = "ع",
                Body = "b", BodyArabic = "ب",
            },
            token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.VipNotifyEmpty, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_non_pr_caller_is_forbidden_on_invitations_list()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await PostAuthAsync(
            "/api/v1/admin/invitations/list",
            new GridQuery { Top = 10 },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublicRelations_role_can_create_invitation()
    {
        var token = await CreatePublicRelationsAndSignInAsync();
        var profile = await SeedVisitorProfileAsync("vvip");

        var response = await PostAuthAsync(
            "/api/v1/admin/invitations",
            new AdminCreateInvitationRequest { SentToUserProfileId = profile.Id },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // -- Helpers ---------------------------------------------------------------

    private async Task<UserProfile> SeedVisitorProfileAsync(string typeKey)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var email = $"vip-{Guid.NewGuid():N}@simf.test";
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = $"VIP {typeKey} {Guid.NewGuid():N}".Substring(0, 24),
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var profileTypeName = typeKey.ToLowerInvariant() switch
        {
            "vvip" => "VVIP",
            "vip" => "VIP",
            "gold" => "Gold",
            _ => "General",
        };
        var profileType = await appDb.ProfileTypes
            .SingleOrDefaultAsync(pt =>
                pt.Name == profileTypeName && pt.UserType == UserType.Visitor);
        if (profileType is null)
        {
            profileType = new UserProfileType
            {
                Id = Guid.NewGuid(),
                Name = profileTypeName,
                NameArabic = profileTypeName,
                PageColor = "#FFD700",
                UserType = UserType.Visitor,
                MobileAppRole = MobileAppRole.None,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.ProfileTypes.Add(profileType);
            await appDb.SaveChangesAsync();
        }

        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileType.Id,
            Name = $"VIP Person {Guid.NewGuid():N}".Substring(0, 24),
            NameArabic = "ضيف كبير",
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            NationalId = "1234567890",
            NationalityId = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.UserProfiles.Add(profile);
        await appDb.SaveChangesAsync();
        return profile;
    }

    private async Task<string> CreateAdministratorAndSignInAsync() =>
        await CreateRoleUserAndSignInAsync(AdministratorRole, UserType.Admin);

    private async Task<string> CreatePublicRelationsAndSignInAsync() =>
        await CreateRoleUserAndSignInAsync(PublicRelationsRole, UserType.Admin);

    private async Task<string> CreateRoleUserAndSignInAsync(string role, UserType userType)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new SimfRole { Name = role });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = $"{role} Test",
                AccountState = AccountState.Approved,
                UserType = userType,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, role);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
