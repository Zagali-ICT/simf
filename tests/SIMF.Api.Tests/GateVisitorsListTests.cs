// D-160 — POST /api/v1/app/gates/{gateId}/visitors/list — cursor-paged
// view of scans at a single gate, backed by the D-158 snapshot columns.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Gates;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class GateVisitorsListTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GateVisitorsListTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task List_returns_scans_with_D158_snapshot_fields_populated()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var qr = await CreateApprovedVisitorWithQrAsync("Ahmad Salem");
        await RecordScanAsync(gate.Id, qr, token);

        var response = await PostListAsync(gate.Id, new PostBody { PageSize = 50 }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Single(body.Items);
        var item = body.Items[0];
        Assert.Equal("Ahmad Salem", item.DisplayName);
        Assert.Equal(ScanOutcome.Allowed, item.Outcome);
        Assert.NotNull(item.UserProfileId);
    }

    [Fact]
    public async Task Cursor_pages_through_scans_in_id_order()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        for (var i = 0; i < 5; i++)
        {
            var qr = await CreateApprovedVisitorWithQrAsync($"Visitor {i}");
            await RecordScanAsync(gate.Id, qr, token);
        }

        var first = await PostListAsync(gate.Id, new PostBody { PageSize = 2 }, token);
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Equal(2, firstBody.Items.Count);
        Assert.NotNull(firstBody.NextCursor);

        var second = await PostListAsync(gate.Id,
            new PostBody { PageSize = 2, Cursor = firstBody.NextCursor }, token);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Equal(2, secondBody.Items.Count);
        Assert.True(secondBody.Items[0].ScanId > firstBody.Items[^1].ScanId);

        var third = await PostListAsync(gate.Id,
            new PostBody { PageSize = 2, Cursor = secondBody.NextCursor }, token);
        var thirdBody = (await third.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Single(thirdBody.Items);
        Assert.Null(thirdBody.NextCursor); // Last partial page → no more
    }

    [Fact]
    public async Task Default_outcome_filter_is_Allowed_so_denied_scans_are_excluded()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        // One allowed scan + one denied (unknown QR).
        var qr = await CreateApprovedVisitorWithQrAsync("Allowed Visitor");
        await RecordScanAsync(gate.Id, qr, token);
        await RecordScanAsync(gate.Id, "ZZZZ99999999", token); // unknown QR → denied

        var response = await PostListAsync(gate.Id, new PostBody { PageSize = 50 }, token);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Single(body.Items);
        Assert.Equal(ScanOutcome.Allowed, body.Items[0].Outcome);
    }

    [Fact]
    public async Task Explicit_outcome_Denied_returns_only_denied_scans()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var qr = await CreateApprovedVisitorWithQrAsync("Allowed Visitor");
        await RecordScanAsync(gate.Id, qr, token);
        await RecordScanAsync(gate.Id, "ZZZZ99999999", token);

        var response = await PostListAsync(gate.Id,
            new PostBody { PageSize = 50, Outcome = ScanOutcome.Denied }, token);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Single(body.Items);
        Assert.Equal(ScanOutcome.Denied, body.Items[0].Outcome);
        Assert.Equal(DenialReasonCode.QrUnknown, body.Items[0].DenialReasonCode);
    }

    [Fact]
    public async Task Operator_not_assigned_to_gate_gets_403()
    {
        var (adminA, _) = await CreateAdminAsync();
        var (adminB, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(adminA); // only admin A is the operator

        var response = await PostListAsync(gate.Id, new PostBody { PageSize = 50 }, adminB);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateOperatorNotAssigned, body.Error!.Code);
    }

    [Fact]
    public async Task Unknown_gate_returns_404_GATE_NOT_FOUND()
    {
        var (token, _) = await CreateAdminAsync();
        var response = await PostListAsync(Guid.NewGuid(),
            new PostBody { PageSize = 50 }, token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.GateNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Malformed_cursor_is_treated_as_no_cursor_and_returns_first_page()
    {
        var (token, _) = await CreateAdminAsync();
        var gate = await CreateGateAsync(token);
        var qr = await CreateApprovedVisitorWithQrAsync("Visitor 1");
        await RecordScanAsync(gate.Id, qr, token);

        var response = await PostListAsync(gate.Id,
            new PostBody { PageSize = 50, Cursor = "!!!not-base64!!!" }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GateVisitorsListResponse>>())!.Data!;
        Assert.Single(body.Items); // first page still returned
    }

    // -- Helpers --------------------------------------------------------------

    private sealed class PostBody
    {
        public string? Cursor { get; set; }
        public int PageSize { get; set; }
        public ScanDirection? Direction { get; set; }
        public ScanOutcome? Outcome { get; set; }
        public DateTime? Since { get; set; }
        public DateTime? Until { get; set; }
    }

    private async Task<HttpResponseMessage> PostListAsync(
        Guid gateId, PostBody body, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/visitors/list")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> RecordScanAsync(
        Guid gateId, string qr, string token)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/app/gates/{gateId}/scans")
        {
            Content = JsonContent.Create(new GateScanRequest
            {
                Qr = qr,
                Source = ScanSource.Simulator,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<AdminGateDetail> CreateGateAsync(string adminToken)
    {
        var code = $"GVL-{Guid.NewGuid().ToString("N")[..8]}";
        var actorId = await GetCurrentAdminUserIdAsync(adminToken);
        var create = await PostAuthAsync(
            "/api/v1/admin/gates",
            new AdminCreateGateRequest
            {
                Code = code,
                Name = "Visitors-list Test Gate",
                NameArabic = "بوابة اختبار قائمة الزوار",
                DirectionMode = DirectionMode.Both,
                AllowedProfileTypeIds = new List<Guid>(),
                AssignedOperatorUserIds = new List<Guid> { actorId },
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content
            .ReadFromJsonAsync<ApiResult<AdminGateDetail>>())!.Data!;
    }

    private async Task<string> CreateApprovedVisitorWithQrAsync(string displayName)
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        var qrId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            QrId = qrId,
            NameArabic = displayName,
            Name = displayName,
            NationalityId = 682,
            PlaceOfBirth = "Riyadh",
            // Admission is owned by the PROFILE (D-877) and defaults to
            // PendingApproval, so a visitor approved only on the account row is
            // refused at the gate with HolderNotApproved - which turned the
            // "allowed" visitor in these fixtures into a second denied scan.
            AdmissionState = AccountState.Approved,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return qrId;
    }

    private async Task<Guid> GetCurrentAdminUserIdAsync(string token)
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token);
        return Guid.Parse(jwt.Claims.First(c => c.Type == "sub" || c.Type == "nameid").Value);
    }

    private async Task<(string Token, string Email)> CreateAdminAsync()
    {
        var email = $"gate-visitors-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Visitors-list Test Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return (await AuthFlow.SignInControlPanelAsync(_client, _factory, email), email);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
