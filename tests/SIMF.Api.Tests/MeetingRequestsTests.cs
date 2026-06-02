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
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
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
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
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
        // D-185: respond returns the detail record (with email) so the
        // CP modal renders the post-respond confirmation directly.
        var responded = (await respond.Content
            .ReadFromJsonAsync<ApiResult<AdminMeetingRequestDetail>>())!.Data!;
        Assert.Equal(MeetingRequestStatus.Accepted, responded.Status);
        Assert.NotNull(responded.RespondedAt);
    }

    [Theory]
    [InlineData("Pending", MeetingRequestStatus.Pending)]
    [InlineData("Accepted", MeetingRequestStatus.Accepted)]
    [InlineData("Rejected", MeetingRequestStatus.Rejected)]
    public async Task Admin_list_filters_by_status_key(
        string filterValue, MeetingRequestStatus expected)
    {
        // D-183 (CP UI for D-174 meeting requests) — the
        // MeetingRequestsList.razor page is the only consumer pinning
        // `Filters["status"] = "Pending"|"Accepted"|"Rejected"`.
        // This test pins that wire shape so a backend rename to
        // `Status` (uppercase) or a switch to enum-int strings breaks
        // the API tests instead of breaking the CP page silently.
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        // Seed three requests in three distinct statuses.
        var pendingId = (await (await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Pending", Subject = "Stays pending",
            }, visitor)).Content.ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!.Id;
        var acceptedId = (await (await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Accept", Subject = "Will be accepted",
            }, visitor)).Content.ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!.Id;
        var rejectedId = (await (await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Reject", Subject = "Will be rejected",
            }, visitor)).Content.ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!.Id;

        var admin = await CreateAdministratorAndSignInAsync();
        await PutAuthAsync(
            $"/api/v1/admin/meeting-requests/{acceptedId}/respond",
            new RespondToMeetingRequestRequest { Status = MeetingRequestStatus.Accepted },
            admin);
        await PutAuthAsync(
            $"/api/v1/admin/meeting-requests/{rejectedId}/respond",
            new RespondToMeetingRequestRequest { Status = MeetingRequestStatus.Rejected },
            admin);

        var list = await PostAuthAsync(
            "/api/v1/admin/meeting-requests/list",
            new GridQuery
            {
                Top = 100,
                Filters = new Dictionary<string, string> { ["status"] = filterValue },
            }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMeetingRequestRow>>>())!.Data!;
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, r => Assert.Equal(expected, r.Status));
    }

    [Fact]
    public async Task List_response_does_not_contain_requester_email()
    {
        // D-185: AdminMeetingRequestRow no longer carries
        // RequesterEmail. The list endpoint MUST NOT serialize an
        // email field — protects against a property re-introduction
        // accidentally re-exposing bulk PII to the CP grid.
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "Visitor", Subject = "T",
            }, visitor);

        var admin = await CreateAdministratorAndSignInAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/meeting-requests/list",
            new GridQuery { Top = 100 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        // D-185 review-pass: empty-page guard — without this the
        // string-contains check is vacuously true on a zero-row page.
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminMeetingRequestRow>>>())!.Data!;
        Assert.NotEmpty(page.Items);
        var raw = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requesterEmail", raw,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_requires_administrator_role()
    {
        // D-185 review-pass (test-analyst): the new PII drill-down
        // endpoint is admin-only. Without this test, a future policy
        // misconfiguration that drops AdministratorOnly would silently
        // expose requester emails to any approved account.
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        var created = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;

        var response = await GetAuthAsync(
            $"/api/v1/admin/meeting-requests/{created.Id}", visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_writes_AdminMeetingRequestsListed_audit_event()
    {
        // D-185 review-pass (test-analyst): SIEM rule M-002 depends on
        // this audit event flowing to OperationLog. Without a test
        // asserting it appears, a future refactor could silently drop
        // the row and the rule would match nothing.
        var (admin, adminId) = await CreateAdministratorAndSignInWithIdAsync();
        var list = await PostAuthAsync(
            "/api/v1/admin/meeting-requests/list",
            new GridQuery { Top = 25 }, admin);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var recorded = await db.OperationLog
            .Where(e => e.EventType == "Admin.MeetingRequestsListed"
                        && e.ActorUserId == adminId)
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(recorded);
        Assert.Equal(AuditOutcome.Success, recorded!.Outcome);
        Assert.NotNull(recorded.Detail);
        Assert.Contains("\"count\"", recorded.Detail!);
        Assert.Contains("\"top\"", recorded.Detail!);
    }

    [Fact]
    public async Task Get_writes_AdminMeetingRequestViewed_audit_event()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        var created = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;

        var (admin, adminId) = await CreateAdministratorAndSignInWithIdAsync();
        await GetAuthAsync($"/api/v1/admin/meeting-requests/{created.Id}", admin);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var recorded = await db.OperationLog
            .Where(e => e.EventType == "Admin.MeetingRequestViewed"
                        && e.ActorUserId == adminId)
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(recorded);
        Assert.NotNull(recorded!.Detail);
        Assert.Contains(created.Id.ToString(), recorded.Detail!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_returns_detail_with_email_for_known_id()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
            new SubmitMeetingRequestRequest
            {
                RequesterName = "V", Subject = "T",
            }, visitor);
        var created = (await submit.Content
            .ReadFromJsonAsync<ApiResult<MeetingRequestSubmitted>>())!.Data!;

        var admin = await CreateAdministratorAndSignInAsync();
        var get = await GetAuthAsync(
            $"/api/v1/admin/meeting-requests/{created.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<AdminMeetingRequestDetail>>())!.Data!;
        Assert.Equal(created.Id, detail.Id);
        Assert.False(string.IsNullOrEmpty(detail.RequesterEmail));
    }

    [Fact]
    public async Task Get_for_unknown_id_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var response = await GetAuthAsync(
            $"/api/v1/admin/meeting-requests/{Guid.NewGuid()}", admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Respond_with_Pending_status_returns_400()
    {
        var session = await SeedActiveSessionAsync();
        var visitor = await SignInApprovedVisitorAsync();
        var submit = await PostAuthAsync(
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
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
            $"/api/v1/app/sessions/{Guid.NewGuid()}/meeting-requests",
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
            $"/api/v1/app/sessions/{session.Id}/meeting-requests",
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
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = AuthFlow.Password, ConfirmPassword = AuthFlow.Password });
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest
            {
                Email = email,
                Code = AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.EmailVerification),
            });
        AuthFlow.SetAccountState(_factory, email, AccountState.Approved);
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var (token, _) = await CreateAdministratorAndSignInWithIdAsync();
        return token;
    }

    private async Task<(string Token, Guid UserId)> CreateAdministratorAndSignInWithIdAsync()
    {
        var email = $"mr-admin-{Guid.NewGuid():N}@simf.test";
        Guid userId;
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
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email, Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (body.Data!.Tokens!.AccessToken, userId);
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

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
