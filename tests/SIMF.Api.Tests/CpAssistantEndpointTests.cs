// Covers the Control Panel operator assistant endpoint: it routes through the
// centralised AI (the cp-assistant prompt), is gated by Assistant.Use, and
// validates its input. The build-wide PermissionEnforcementTests reflection
// sweep already proves it is permission + approval gated; these add the
// behavioural checks.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class CpAssistantEndpointTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string Route = "/api/v1/admin/ai/assistant";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public CpAssistantEndpointTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Administrator_gets_cp_assistant_answer_and_records_admin_caller()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(Route, new CpAssistantRequest
        {
            Question = "Where do I add a session?",
            Pages = "## Programme\n- Sessions -> /admin/sessions",
            Locale = "en",
        }, admin);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<AiCallResult>>())!.Data!;
        Assert.Equal("cp-assistant", body.PromptKey);
        Assert.Equal(AiFeature.CpAssistant, body.Feature);
        Assert.Equal(AiProvider.Echo, body.Provider);
        // The Echo provider echoes the substituted user prompt, which carries
        // the operator's question and the grounding route.
        Assert.Contains("Where do I add a session?", body.OutputText);
        Assert.Contains("/admin/sessions", body.OutputText);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var invocation = await db.AiInvocations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == body.InvocationId);
        Assert.NotNull(invocation);
        Assert.Equal("Admin", invocation!.CallerKind);
        Assert.Null(invocation.ErrorCode);
    }

    [Fact]
    public async Task Admin_without_the_permission_is_forbidden()
    {
        // An admin whose only role grants no permissions — no Assistant.Use,
        // no wildcard.
        var limited = await CreateAdminWithEmptyRoleAsync();

        var response = await PostAuthAsync(Route, new CpAssistantRequest
        {
            Question = "Where do I add a session?",
        }, limited);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Empty_question_returns_400_AI_INPUT_INVALID()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(Route, new CpAssistantRequest
        {
            Question = "   ",
            Pages = "## Programme\n- Sessions -> /admin/sessions",
        }, admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AiInputInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"cpa-admin-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "CPA Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await SignInCpAsync(email);
    }

    private async Task<string> CreateAdminWithEmptyRoleAsync()
    {
        var email = $"cpa-limited-{Guid.NewGuid():N}@simf.test";
        var roleName = $"NoPerms-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            await roles.CreateAsync(new SimfRole { Name = roleName, IsBaseline = false });
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "CPA Limited",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, roleName);
        }
        return await SignInCpAsync(email);
    }

    private async Task<string> SignInCpAsync(string email)
    {
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
}
