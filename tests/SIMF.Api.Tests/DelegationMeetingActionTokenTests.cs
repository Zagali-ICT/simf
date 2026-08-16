// Tests: R4 (bi-meeting rules, D-767) — delegation confirm-by-link action tokens.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.MeetingRequests;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>R4 (D-767) — the delegation confirm-by-link token: emailed to every target
/// member on Approve, the first click flips AwaitingSpeaker → Accepted (mirroring the
/// in-app tap). It rides the SAME public /app/meeting-actions/{token} endpoints as the
/// speaker links, so these assert the shared endpoint also serves a delegation token.</summary>
[Trait(TestAreas.TraitName, TestAreas.Meetings)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class DelegationMeetingActionTokenTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public DelegationMeetingActionTokenTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Confirm_link_books_the_meeting_and_marks_the_token_used()
    {
        var (requestId, requestingName) = await SeedAwaitingConfirmRequestAsync();
        var token = await MintConfirmAsync(requestId);

        // GET preview is safe + shows the requesting delegation as the requester.
        var preview = await PreviewAsync(token);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = (await preview.Content
            .ReadFromJsonAsync<ApiResult<MeetingActionPreview>>())!.Data!;
        Assert.Equal(MeetingActionType.Approve, body.Action);
        Assert.Equal(requestingName, body.RequesterName);
        Assert.Equal("Naval cooperation", body.Subject);
        Assert.Equal("Meeting Hall", body.HallName);

        // POST confirms — AwaitingSpeaker → Accepted, token consumed.
        var confirm = await ConfirmAsync(token);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var outcome = (await confirm.Content
            .ReadFromJsonAsync<ApiResult<MeetingActionOutcome>>())!.Data!;
        Assert.Equal(MeetingActionType.Approve, outcome.Action);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Accepted, req.Status);
        Assert.NotNull(req.ConfirmedAt);
        Assert.True(await db.DelegationMeetingActionTokens.AsNoTracking()
            .AnyAsync(t => t.DelegationMeetingRequestId == requestId && t.UsedAt != null));
    }

    [Fact]
    public async Task A_used_confirm_link_is_a_neutral_404()
    {
        var (requestId, _) = await SeedAwaitingConfirmRequestAsync();
        var token = await MintConfirmAsync(requestId);

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(token)).StatusCode);
        // Single-use: the same link again → neutral 404 (the request left AwaitingSpeaker).
        await AssertNeutralInvalidAsync(await ConfirmAsync(token));
        await AssertNeutralInvalidAsync(await PreviewAsync(token));
    }

    [Fact]
    public async Task Concurrent_double_confirm_applies_exactly_once()
    {
        // Two members click the same emailed link at once → exactly one books the meeting,
        // the other is the neutral 404; the request lands on a single Accepted.
        var (requestId, _) = await SeedAwaitingConfirmRequestAsync();
        var token = await MintConfirmAsync(requestId);

        var responses = await Task.WhenAll(ConfirmAsync(token), ConfirmAsync(token));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Accepted, req.Status);
    }

    [Fact]
    public async Task An_expired_confirm_link_is_a_neutral_404_and_does_not_book()
    {
        // B9 — the 72h TTL is the only thing standing between an emailed link and an
        // indefinitely valid credential, so expiry must be enforced on BOTH the GET
        // preview and the POST apply.
        var (requestId, _) = await SeedAwaitingConfirmRequestAsync();
        var token = await MintConfirmAsync(requestId);
        await ExpireTokensAsync(requestId);

        await AssertNeutralInvalidAsync(await PreviewAsync(token));
        await AssertNeutralInvalidAsync(await ConfirmAsync(token));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.DelegationMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.AwaitingSpeaker, req.Status);
    }

    [Fact]
    public async Task A_secret_that_matches_no_token_of_either_kind_is_a_neutral_404()
    {
        // B9 — one public endpoint serves the SPEAKER and the DELEGATION token tables.
        // A secret shaped like a token but belonging to neither must be the same
        // neutral 404, never a hint about which family it failed to match.
        var secret = MeetingActionTokenHasher.NewSecret();

        await AssertNeutralInvalidAsync(await PreviewAsync(secret));
        await AssertNeutralInvalidAsync(await ConfirmAsync(secret));
    }

    [Fact]
    public async Task A_delegation_secret_cannot_decide_a_speaker_request()
    {
        // B9 (wrong-type) — a delegation confirm token whose request left
        // AwaitingSpeaker is dead; it must not fall through to the speaker branch or
        // touch any speaker request.
        var (requestId, _) = await SeedAwaitingConfirmRequestAsync();
        var token = await MintConfirmAsync(requestId);
        await SetStatusAsync(requestId, MeetingRequestStatus.Cancelled);

        await AssertNeutralInvalidAsync(await PreviewAsync(token));
        await AssertNeutralInvalidAsync(await ConfirmAsync(token));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.Equal(MeetingRequestStatus.Cancelled,
            (await db.DelegationMeetingRequests.AsNoTracking()
                .SingleAsync(r => r.Id == requestId)).Status);
        // The token was NOT consumed — an unusable token is never claimed.
        Assert.False(await db.DelegationMeetingActionTokens.AsNoTracking()
            .AnyAsync(t => t.DelegationMeetingRequestId == requestId && t.UsedAt != null));
    }

    // -- helpers --------------------------------------------------------------

    private async Task ExpireTokensAsync(Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        await db.DelegationMeetingActionTokens
            .Where(t => t.DelegationMeetingRequestId == requestId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                t => t.ExpiresAt, SimfClock.Now.AddHours(-1)));
    }

    private async Task SetStatusAsync(Guid requestId, MeetingRequestStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        await db.DelegationMeetingRequests
            .Where(r => r.Id == requestId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status));
    }

    private Task<HttpResponseMessage> PreviewAsync(string token) =>
        _client.GetAsync($"/api/v1/app/meeting-actions/{Uri.EscapeDataString(token)}");

    private Task<HttpResponseMessage> ConfirmAsync(string token) =>
        _client.PostAsync($"/api/v1/app/meeting-actions/{Uri.EscapeDataString(token)}", content: null);

    private static async Task AssertNeutralInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingActionTokenInvalid, body.Error!.Code);
    }

    private async Task<string> MintConfirmAsync(Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMeetingActionTokenService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // StageDelegationConfirmToken adds the row to the (shared, scoped) context without
        // saving; the caller commits — mirrors how RespondAsync commits it with the
        // AwaitingSpeaker transition.
        var url = service.StageDelegationConfirmToken(requestId);
        await db.SaveChangesAsync();
        Assert.False(string.IsNullOrEmpty(url));
        return ExtractToken(url);
    }

    private static string ExtractToken(string url)
    {
        var marker = url.IndexOf("token=", StringComparison.Ordinal);
        return Uri.UnescapeDataString(url[(marker + "token=".Length)..]);
    }

    private async Task<(Guid RequestId, string RequestingName)> SeedAwaitingConfirmRequestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Country.Code is ISO alpha-2 (nvarchar(2)); get-or-create by code is idempotent
        // and shared-DB-safe. FR/DE keep clear of the SA/EG/US the delegation-flow tests use.
        var requesting = await EnsureCountryAsync(db, "FR", 250);
        var target = await EnsureCountryAsync(db, "DE", 276);

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MH-" + suffix,
            Name = "Meeting Hall", NameArabic = "قاعة الاجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        var start = new DateTime(2031, 6, 1, 10, 0, 0);
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = requesting.Id,
            TargetCountryId = target.Id,
            AttendeeCount = 3,
            Subject = "Naval cooperation",
            HallId = hall.Id, SlotStart = start, SlotEnd = start.AddMinutes(30),
            Status = MeetingRequestStatus.AwaitingSpeaker,
            CreatedAt = SimfClock.Now, RespondedAt = SimfClock.Now,
        };
        db.DelegationMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return (req.Id, requesting.Name);
    }

    private static async Task<Country> EnsureCountryAsync(SimfAppDbContext db, string code, int id)
    {
        var country = await db.Countries.FirstOrDefaultAsync(c => c.Code == code);
        if (country is null)
        {
            country = new Country
            {
                Id = id, Code = code, Name = code, NameArabic = code,
                IsActive = true, IsInvited = true, CreatedAt = SimfClock.Now,
            };
            db.Countries.Add(country);
            await db.SaveChangesAsync();
        }
        return country;
    }
}
