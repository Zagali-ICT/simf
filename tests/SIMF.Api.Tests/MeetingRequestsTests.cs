// D-174 (gap doc G11, Mockup page 27) — MeetingRequest submit + admin respond.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Sessions;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MeetingRequestsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingRequestsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Submit_returns_pending_status()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Captain Ahmed",
                Subject = "I'd like to discuss naval cybersecurity.",
            },
            visitor);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var body = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Pending, body.Status);
    }

    [Fact]
    public async Task Admin_lists_then_responds_with_Accepted()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Visitor", Subject = "Test topic",
            },
            visitor);
        var created = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;

        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/meeting-requests/list",
            new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMeetingRequestRow>>>())!.Data!;
        Assert.Contains(page.Items, r => r.Id == created.Id);

        var respond = await PutAuthAsync(
            $"/api/v1/admin/meeting-requests/{created.Id}/respond",
            new RespondToMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Accepted,
                ResponseNote = "Confirmed for tomorrow at 10am.",
            }, admin);
        Assert.Equal(HttpStatusCode.OK, respond.StatusCode);
        var responded = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminMeetingRequestRow>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Accepted, responded.Status);
        Assert.NotNull(responded.RespondedAt);
    }

    [Fact]
    public async Task Respond_with_Pending_status_returns_400()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        var created = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;

        var admin = await CreateAdministratorAndSignInAsync();
        var respond = await PutAuthAsync(
            $"/api/v1/admin/meeting-requests/{created.Id}/respond",
            new RespondToMeetingRequestRequest
            {
                Status = MeetingRequestStatus.Pending,
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, respond.StatusCode);
        var body = (await respond.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingRequestStatusInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_to_unknown_session_is_400()
    {
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/sessions/{Guid.NewGuid()}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingRequestSessionNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Submit_with_empty_subject_is_MEETING_REQUEST_INVALID()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var response = await PostAuthAsync(
            $"/api/v1/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Captain", Subject = "  ",
            }, visitor);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingRequestInvalid, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Session> SeedActiveSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Hall", NameArabic = "قاعة",
            Capacity = 100, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Live", TitleArabic = "مباشر",
            HallId = hall.Id,
            StartUtc = DateTimeOffset.UtcNow.AddMinutes(-15),
            EndUtc = DateTimeOffset.UtcNow.AddMinutes(45),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    private async Task<string> SignInApprovedVisitorAsync()
    {
        var email = $"mr-visitor-{Guid.NewGuid():N}@simf.test";
        await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"mr-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "MR Admin",
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
                Email = email, Password = AuthFlow.Password,
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
