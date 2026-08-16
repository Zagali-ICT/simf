// P3.2a — D-231 (Completion Programme §5.2, broadcast Option A): the
// session broadcast lifecycle (Scheduled → Held → Recorded → Published)
// driven by the Scientific Committee via Sessions.Publish. Mirrors
// AdminSessionsTests' setup; asserts the legal transitions, the illegal
// jump guard, the PublishedAt stamp, and the public read.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Files.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SessionLifecycleTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionLifecycleTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task New_session_starts_scheduled()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        Assert.Equal(SessionStatus.Scheduled, session.Status);
        Assert.Null(session.PublishedAt);
    }

    [Fact]
    public async Task Lifecycle_advances_to_published_and_stamps_publishedAt()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        // S-7 — Recorded/Published require an attached recording.
        await StampRecordingAsync(session.Id);

        var held = await SetStatusAsync(session.Id, SessionStatus.Held, token);
        Assert.Equal(SessionStatus.Held, await ReadStatusAsync(held));

        var recorded = await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        Assert.Equal(SessionStatus.Recorded, await ReadStatusAsync(recorded));

        var publishResponse = await SetStatusAsync(session.Id, SessionStatus.Published, token);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        var published = (await publishResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Published, published.Status);
        Assert.NotNull(published.PublishedAt);

        // The public read reflects the published state.
        var pub = await _client.GetAsync($"/api/v1/app/programme/sessions/{session.Id}");
        Assert.Equal(HttpStatusCode.OK, pub.StatusCode);
        var detail = (await pub.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Published, detail.Status);
        Assert.NotNull(detail.PublishedAt);
    }

    [Fact]
    public async Task Skipping_a_step_is_400_SESSION_STATUS_TRANSITION_INVALID()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var response = await SetStatusAsync(session.Id, SessionStatus.Published, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionStatusTransitionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Unpublishing_clears_publishedAt()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        // S-7 — Recorded/Published require an attached recording.
        await StampRecordingAsync(session.Id);
        await SetStatusAsync(session.Id, SessionStatus.Held, token);
        await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        await SetStatusAsync(session.Id, SessionStatus.Published, token);

        var unpublishResponse = await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        Assert.Equal(HttpStatusCode.OK, unpublishResponse.StatusCode);
        var detail = (await unpublishResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Recorded, detail.Status);
        Assert.Null(detail.PublishedAt);
    }

    [Fact]
    public async Task Setting_the_same_status_is_an_idempotent_noop()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);

        var response = await SetStatusAsync(session.Id, SessionStatus.Scheduled, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Scheduled, detail.Status);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_set_status()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(adminToken);

        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var response = await SetStatusAsync(session.Id, SessionStatus.Held, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- S-7: clock + recording guards on SetStatus ---------------------------

    // S-7 — a session cannot be marked Held before it has started (clock guard).
    [Fact]
    public async Task SetStatusAsync_MarkHeldBeforeStart_ReturnsBadRequest()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var future = SimfClock.Now.AddHours(2);
        var session = await CreateSessionAsync(token, future, future.AddHours(1));

        var response = await SetStatusAsync(session.Id, SessionStatus.Held, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionStatusGuardFailed, body.Error!.Code);
    }

    [Fact]
    public async Task SetStatusAsync_MarkHeldAfterStart_Succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token); // default past start

        var response = await SetStatusAsync(session.Id, SessionStatus.Held, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Held, detail.Status);
    }

    // S-7 — Recorded/Published require an attached recording (recording guard).
    [Fact]
    public async Task SetStatusAsync_MarkRecordedWithoutRecording_ReturnsBadRequest()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token); // past start, no recording
        await SetStatusAsync(session.Id, SessionStatus.Held, token);

        var response = await SetStatusAsync(session.Id, SessionStatus.Recorded, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionStatusGuardFailed, body.Error!.Code);
    }

    // S-7 — a reverse (undo) move carries no guard.
    [Fact]
    public async Task SetStatusAsync_RevertRecordedToHeld_NoGuard()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var session = await CreateSessionAsync(token);
        await StampRecordingAsync(session.Id);
        await SetStatusAsync(session.Id, SessionStatus.Held, token);
        await SetStatusAsync(session.Id, SessionStatus.Recorded, token);

        var revert = await SetStatusAsync(session.Id, SessionStatus.Held, token);
        Assert.Equal(HttpStatusCode.OK, revert.StatusCode);
        var detail = (await revert.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(SessionStatus.Held, detail.Status);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<SessionStatus> ReadStatusAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        return detail.Status;
    }

    private Task<HttpResponseMessage> SetStatusAsync(Guid id, SessionStatus status, string token) =>
        PutAuthAsync($"/api/v1/admin/sessions/{id}/status",
            new SetSessionStatusRequest { Status = status }, token);

    private async Task<AdminSessionDetail> CreateSessionAsync(
        string token, DateTime? start = null, DateTime? end = null)
    {
        var hall = await SeedHallAsync(capacity: 100);
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
                Title = "Lifecycle session",
                TitleArabic = "جلسة دورة الحياة",
                HallId = hall.Id,
                // #3 — an Event stays valid under the required-type rule with no speaker.
                Type = SessionType.Event,
                // S-7 — default to a past start so the Held lifecycle guard passes;
                // callers testing the pre-start guard pass an explicit future start.
                Start = start ?? SimfClock.Now.AddHours(-1),
                End = end ?? SimfClock.Now.AddHours(1),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
    }

    // S-7 — satisfy the Recorded/Published guard without streaming a real video
    // (this suite asserts the lifecycle transitions, not the bytes).
    //
    // A fabricated Guid used to be enough here. RecordingFileId is now a real
    // foreign key into StoredFiles, so the file has to exist: the fixture creates
    // one through the ordinary service and points the session at it.
    private async Task StampRecordingAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();

        // CreateStreamedAsync, not UploadAsync: it is the path the real upload
        // endpoint takes for recordings, and it takes the extension explicitly
        // rather than sniffing magic bytes out of a payload.
        using var content = new MemoryStream("FAKE-MP4-RECORDING-BYTES"u8.ToArray());
        var stored = await files.CreateStreamedAsync(
            FileService.SessionRecording, sessionId, content, "lifecycle.mp4",
            "video/mp4", ".mp4", Guid.Empty, CancellationToken.None);

        var session = await db.Sessions.FindAsync(sessionId);
        session!.RecordingFileId = stored.Id;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_recording_pointer_to_a_file_that_does_not_exist_is_refused()
    {
        // The guarantee the foreign key buys, pinned rather than assumed. Until it
        // existed this suite's own fixture stamped an arbitrary Guid, and the
        // database accepted a session pointing at a recording nobody had uploaded.
        var hall = await SeedHallAsync(capacity: 10);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.Sessions.Add(new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "FK guard", TitleArabic = "حارس المفتاح",
            HallId = hall.Id,
            Start = SimfClock.Now.AddMinutes(-15),
            End = SimfClock.Now.AddMinutes(45),
            RecordingFileId = Guid.NewGuid(), // no StoredFile row behind it
            IsActive = true, CreatedAt = SimfClock.Now,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<Hall> SeedHallAsync(int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Lifecycle Hall",
            NameArabic = "قاعة دورة الحياة",
            Capacity = capacity,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        await db.SaveChangesAsync();
        return hall;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"lifecycle-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Lifecycle Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
