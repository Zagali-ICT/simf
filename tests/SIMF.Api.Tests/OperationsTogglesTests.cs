// D-166 (gap doc G4, PDF §2.3 + §2.4) — registration gate +
// archive visibility singletons. Covers admin GET/PUT, public archive
// visibility, sign-up rejection when the gate is closed, and the
// idempotent "no-op when unchanged" path.
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
using SIMF.Domain.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class OperationsTogglesTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public OperationsTogglesTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_GET_returns_the_seeded_open_state()
    {
        await ResetGateOpenAsync();
        var token = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync("/api/v1/admin/registration-gate", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = (await response.Content
            .ReadFromJsonAsync<ApiResult<RegistrationGateState>>())!.Data!;
        Assert.True(state.IsOpen);
    }

    [Fact]
    public async Task Closing_the_gate_makes_sign_up_return_REGISTRATION_CLOSED()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var close = await PutAuthAsync(
            "/api/v1/admin/registration-gate",
            new UpdateRegistrationGateRequest { IsOpen = false, AutoClose = null },
            token);
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        try
        {
            var signUp = await _client.PostAsJsonAsync(
                "/api/v1/app/auth/sign-up",
                new SignUpRequest
                {
                    Email = $"gated-{Guid.NewGuid():N}@simf.test",
                    Password = AuthFlow.Password,
                    ConfirmPassword = AuthFlow.Password,
                });
            Assert.Equal(HttpStatusCode.Forbidden, signUp.StatusCode);
            var body = (await signUp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.RegistrationClosed, body.Error!.Code);
        }
        finally
        {
            await ResetGateOpenAsync();
        }
    }

    [Fact]
    public async Task Sign_up_succeeds_when_the_gate_is_open()
    {
        await ResetGateOpenAsync();
        var signUp = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest
            {
                Email = $"open-{Guid.NewGuid():N}@simf.test",
                Password = AuthFlow.Password,
                ConfirmPassword = AuthFlow.Password,
            });
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);
    }

    [Fact]
    public async Task Auto_close_in_the_past_blocks_sign_up()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var setAutoClose = await PutAuthAsync(
            "/api/v1/admin/registration-gate",
            new UpdateRegistrationGateRequest
            {
                IsOpen = true,
                AutoClose = DateTimeOffset.UtcNow.AddMinutes(-1),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, setAutoClose.StatusCode);

        try
        {
            var signUp = await _client.PostAsJsonAsync(
                "/api/v1/app/auth/sign-up",
                new SignUpRequest
                {
                    Email = $"past-{Guid.NewGuid():N}@simf.test",
                    Password = AuthFlow.Password,
                    ConfirmPassword = AuthFlow.Password,
                });
            Assert.Equal(HttpStatusCode.Forbidden, signUp.StatusCode);
            var body = (await signUp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
            Assert.Equal(ErrorCodes.RegistrationClosed, body.Error!.Code);
        }
        finally
        {
            await ResetGateOpenAsync();
        }
    }

    [Fact]
    public async Task Public_archive_visibility_endpoint_requires_no_auth()
    {
        var response = await _client.GetAsync("/api/v1/app/archive/visibility");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = (await response.Content
            .ReadFromJsonAsync<ApiResult<ArchiveVisibilityState>>())!.Data!;
        Assert.True(state.IsVisible);
    }

    [Fact]
    public async Task Admin_can_toggle_archive_visibility_and_public_endpoint_reflects_it()
    {
        var token = await CreateAdministratorAndSignInAsync();
        try
        {
            var hide = await PutAuthAsync(
                "/api/v1/admin/archive/visibility",
                new UpdateArchiveVisibilityRequest { IsVisible = false },
                token);
            Assert.Equal(HttpStatusCode.OK, hide.StatusCode);

            var publicGet = await _client.GetAsync("/api/v1/app/archive/visibility");
            var state = (await publicGet.Content
                .ReadFromJsonAsync<ApiResult<ArchiveVisibilityState>>())!.Data!;
            Assert.False(state.IsVisible);
        }
        finally
        {
            // Restore the visible state so later tests are not affected.
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var row = await db.ArchiveVisibility
                .SingleAsync(a => a.Id == ArchiveVisibility.SingletonId);
            row.IsVisible = true;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Non_admin_caller_cannot_toggle_either_gate()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var gate = await PutAuthAsync(
            "/api/v1/admin/registration-gate",
            new UpdateRegistrationGateRequest { IsOpen = false },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, gate.StatusCode);

        var archive = await PutAuthAsync(
            "/api/v1/admin/archive/visibility",
            new UpdateArchiveVisibilityRequest { IsVisible = false },
            tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task ResetGateOpenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = await db.RegistrationGate
            .SingleOrDefaultAsync(g => g.Id == RegistrationGate.SingletonId);
        if (row is null)
        {
            db.RegistrationGate.Add(new RegistrationGate
            {
                Id = RegistrationGate.SingletonId,
                IsOpen = true,
                AutoClose = null,
                LastChangedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            row.IsOpen = true;
            row.AutoClose = null;
        }
        await db.SaveChangesAsync();
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"ops-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Ops Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
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
}
