// Tests: D-500 (Wave 5, الطلبات "طلب وثيقة المشاركة") — participation-document
// request submission + admin review (ParticipationDocumentRequestService +
// ParticipationDocumentRequestEndpoints).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Requests;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ParticipationDocumentRequestsTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ParticipationDocumentRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submission_requires_authentication()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/app/document-requests", new { documentType = 0 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_creates_a_pending_request()
    {
        var token = await SignInApprovedVisitorAsync();
        var submitted = await SubmitAsync(token, documentType: 0, note: "For my employer.");
        Assert.Equal(MeetingRequestStatus.Pending, submitted.Status);
        Assert.NotEqual(Guid.Empty, submitted.Id);
    }

    [Fact]
    public async Task Admin_can_list_then_accept_a_request()
    {
        var token = await SignInApprovedVisitorAsync();
        var submitted = await SubmitAsync(token, documentType: 1);
        var admin = await CreateAdministratorAndSignInAsync();

        var list = await SendAuthAsync(HttpMethod.Post, "/api/v1/admin/document-requests/list",
            admin, new GridQuery { Top = 50 });
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminParticipationDocumentRequestRow>>>())!;
        Assert.Contains(page.Data!.Items, r => r.Id == submitted.Id);

        var respond = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/document-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Accepted, responseNote = "Issued." });
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var detail = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminParticipationDocumentRequestDetail>>())!;
        Assert.Equal(MeetingRequestStatus.Accepted, detail.Data!.Status);
    }

    [Fact]
    public async Task Admin_list_requires_permission()
    {
        var token = await SignInApprovedVisitorAsync();
        var response = await SendAuthAsync(HttpMethod.Post,
            "/api/v1/admin/document-requests/list", token, new GridQuery());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Responding_with_pending_status_is_rejected()
    {
        var token = await SignInApprovedVisitorAsync();
        var submitted = await SubmitAsync(token, documentType: 0);
        var admin = await CreateAdministratorAndSignInAsync();

        var respond = await SendAuthAsync(HttpMethod.Put,
            $"/api/v1/admin/document-requests/{submitted.Id}/respond", admin,
            new { status = (int)MeetingRequestStatus.Pending });
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
    }

    // -- helpers --------------------------------------------------------------

    private async Task<ParticipationDocumentRequestSubmitted> SubmitAsync(
        string token, int documentType, string? note = null)
    {
        var response = await SendAuthAsync(HttpMethod.Post, "/api/v1/app/document-requests",
            token, new { documentType, note });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<ParticipationDocumentRequestSubmitted>>())!.Data!;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"doc-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync("/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync("/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        AuthFlow.DisableTwoFactor(_factory, email);
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!
            .Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"doc-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Doc Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password, Audience = SignInAudience.Cp });
        return (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!
            .Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method, string url, string token, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
