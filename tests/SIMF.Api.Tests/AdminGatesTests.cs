// Tests cover: GET/POST/PUT/DELETE on /api/v1/admin/gates/*; auth gate;
// validation; duplicate code conflict; logical FK validation for the
// allowed-profile-types + assigned-operators lists. D-148 (Gate Module).
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
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AdminGatesTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AdminGatesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_creates_lists_gets_a_general_gate_with_no_allow_list()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = $"GA-{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "General Gate",
                NameArabic = "بوابة عامة",
                DirectionMode = DirectionMode.Both,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var detail = (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
        Assert.Equal(code.ToUpperInvariant(), detail.Code);
        Assert.True(detail.IsActive);
        Assert.Empty(detail.AllowedProfileTypeIds);

        var list = await PostAuthAsync(
            "/api/v1/admin/gates/list",
            new GridQuery { Top = 50 },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminGateSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Id == detail.Id);

        var get = await GetAuthAsync($"/api/v1/admin/gates/{detail.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Duplicate_code_is_a_409_GATE_CODE_DUPLICATE()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = $"DUP-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

        var first = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "First Gate",
                NameArabic = "البوابة الأولى",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Second Gate",
                NameArabic = "البوابة الثانية",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Update_changes_DirectionMode_and_can_deactivate()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();

        var created = await CreateGateAsync(adminToken, DirectionMode.In);
        var update = await PutAuthAsync(
            $"/api/v1/admin/gates/{created.Id}",
            new AdminUpdateGateRequest
            {
                Code = created.Code,
                Name = created.Name,
                NameArabic = created.NameArabic,
                DirectionMode = DirectionMode.Out,
                IsActive = false,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
        Assert.Equal(DirectionMode.Out, updated.DirectionMode);
        Assert.False(updated.IsActive);

        var del = await DeleteAuthAsync($"/api/v1/admin/gates/{created.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
    }

    [Fact]
    public async Task Get_returns_404_for_unknown_id()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/gates/{Guid.NewGuid()}", adminToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_create()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = "FORBID",
                Name = "Should Not Land",
                NameArabic = "لن يُسجَّل",
            },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_code_length_is_a_400_GATE_INVALID()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = "X", // 1 char — below the 2-char minimum
                Name = "Too short",
                NameArabic = "قصير جداً",
            },
            adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Unknown_profile_type_id_is_a_400_GATE_PROFILE_TYPE_INVALID()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"PT-{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                Name = "Gate with bad pt",
                NameArabic = "بوابة سيئة",
                AllowedProfileTypeIds = new List<Guid> { Guid.NewGuid() },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Unknown_operator_user_id_is_a_400_GATE_ASSIGNMENT_INVALID()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var response = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = $"OP-{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                Name = "Gate with bad op",
                NameArabic = "بوابة سيئة",
                AssignedOperatorUserIds = new List<Guid> { Guid.NewGuid() },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateAssignmentInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken, DirectionMode mode)
    {
        var code = $"H-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Helper Gate",
                NameArabic = "بوابة مساعدة",
                DirectionMode = mode,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"gate-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Gate Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
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
