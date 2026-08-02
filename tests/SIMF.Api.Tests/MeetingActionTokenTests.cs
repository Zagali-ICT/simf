// Tests: D-717 (item 7, FDS-013 §15.7 GAP-3) — speaker double-opt-in action tokens.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class MeetingActionTokenTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingActionTokenTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Mint_creates_one_approve_and_one_reject_token()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        await MintAsync(requestId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var tokens = await db.MeetingActionTokens.AsNoTracking()
            .Where(t => t.SpeakerMeetingRequestId == requestId)
            .ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, t => t.Action == MeetingActionType.Approve);
        Assert.Single(tokens, t => t.Action == MeetingActionType.Reject);
        Assert.All(tokens, t => Assert.Null(t.UsedAt));
    }

    [Fact]
    public async Task Preview_is_GET_safe_and_does_not_consume_the_token()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, _) = await MintAsync(requestId);

        // Two previews (as a link prefetcher would do) — both succeed and neither
        // consumes the token.
        var first = await PreviewAsync(approve);
        var second = await PreviewAsync(approve);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var preview = (await second.Content
            .ReadFromJsonAsync<ApiResult<MeetingActionPreview>>())!.Data!;
        Assert.Equal(MeetingActionType.Approve, preview.Action);
        Assert.Equal("Naval cooperation", preview.Subject);
        Assert.Equal("Meeting Hall", preview.HallName);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        Assert.False(await db.MeetingActionTokens.AsNoTracking()
            .AnyAsync(t => t.SpeakerMeetingRequestId == requestId && t.UsedAt != null));
    }

    [Fact]
    public async Task Approve_confirms_the_meeting_and_marks_the_token_used()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, _) = await MintAsync(requestId);

        var confirm = await ConfirmAsync(approve);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var outcome = (await confirm.Content
            .ReadFromJsonAsync<ApiResult<MeetingActionOutcome>>())!.Data!;
        Assert.Equal(MeetingActionType.Approve, outcome.Action);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Accepted, req.Status);
        Assert.NotNull(req.SpeakerDecisionAt);
        Assert.True(await db.MeetingActionTokens.AsNoTracking()
            .AnyAsync(t => t.SpeakerMeetingRequestId == requestId
                && t.Action == MeetingActionType.Approve && t.UsedAt != null));
    }

    [Fact]
    public async Task Reject_declines_the_meeting()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (_, reject) = await MintAsync(requestId);

        var confirm = await ConfirmAsync(reject);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking()
            .SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Rejected, req.Status);
    }

    [Fact]
    public async Task A_used_token_and_its_sibling_are_neutral_404s()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, reject) = await MintAsync(requestId);

        Assert.Equal(HttpStatusCode.OK, (await ConfirmAsync(approve)).StatusCode);

        // The same token again → neutral 404 (single-use).
        await AssertNeutralInvalidAsync(await ConfirmAsync(approve));
        // The sibling Reject token → also dead: the request left AwaitingSpeaker.
        await AssertNeutralInvalidAsync(await ConfirmAsync(reject));
        await AssertNeutralInvalidAsync(await PreviewAsync(reject));
    }

    [Fact]
    public async Task Concurrent_double_confirm_of_the_same_token_applies_exactly_once()
    {
        // D-717 (Finding 1) — the atomic single-use guard: two concurrent POSTs of the
        // same token (double-submit / retry) → exactly one applies, one is the neutral
        // 404, and the requester is not double-notified.
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, _) = await MintAsync(requestId);

        var responses = await Task.WhenAll(ConfirmAsync(approve), ConfirmAsync(approve));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        Assert.Equal(MeetingRequestStatus.Accepted, req.Status);
    }

    [Fact]
    public async Task Concurrent_approve_and_reject_yield_exactly_one_deterministic_decision()
    {
        // D-717 (Finding 1) — the sibling race: the speaker confirms Approve AND Reject
        // at once. Exactly one wins; the request lands on a single terminal decision
        // (never both, never stuck AwaitingSpeaker).
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, reject) = await MintAsync(requestId);

        var responses = await Task.WhenAll(ConfirmAsync(approve), ConfirmAsync(reject));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NotFound));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var req = await db.SpeakerMeetingRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        Assert.True(req.Status is MeetingRequestStatus.Accepted or MeetingRequestStatus.Rejected);
    }

    [Fact]
    public async Task An_unknown_token_is_a_neutral_404()
    {
        await AssertNeutralInvalidAsync(await PreviewAsync("not-a-real-token"));
        await AssertNeutralInvalidAsync(await ConfirmAsync("not-a-real-token"));
    }

    [Fact]
    public async Task An_expired_token_is_a_neutral_404()
    {
        var (requestId, _) = await SeedAwaitingSpeakerRequestAsync();
        var (approve, _) = await MintAsync(requestId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var tokens = await db.MeetingActionTokens
                .Where(t => t.SpeakerMeetingRequestId == requestId)
                .ToListAsync();
            foreach (var t in tokens)
            {
                t.Expires = new DateTime(2020, 1, 1, 0, 0, 0);
            }
            await db.SaveChangesAsync();
        }

        await AssertNeutralInvalidAsync(await PreviewAsync(approve));
    }

    // -- helpers --------------------------------------------------------------

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

    private async Task<(string Approve, string Reject)> MintAsync(Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMeetingActionTokenService>();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        // StageTokensForRequest adds the rows to the (shared, scoped) context without
        // saving; the caller commits — mirrors how RespondAsync commits them with the
        // AwaitingSpeaker transition.
        var links = service.StageTokensForRequest(requestId);
        await db.SaveChangesAsync();
        Assert.True(links.HasUrls);
        return (ExtractToken(links.ApproveUrl), ExtractToken(links.RejectUrl));
    }

    private static string ExtractToken(string url)
    {
        var marker = url.IndexOf("token=", StringComparison.Ordinal);
        return Uri.UnescapeDataString(url[(marker + "token=".Length)..]);
    }

    private async Task<(Guid RequestId, Guid SpeakerId)> SeedAwaitingSpeakerRequestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Capt. Rashid", NameArabic = "راشد",
            AllowsMeetingRequests = true, IsActive = true, DisplayOrder = 0,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "MH-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall", NameArabic = "قاعة الاجتماعات",
            Purpose = HallPurpose.Meeting, Capacity = 10, IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        var start = new DateTime(2031, 6, 1, 10, 0, 0);
        var req = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Capt. Visitor", Subject = "Naval cooperation",
            HallId = hall.Id, SlotStart = start, SlotEnd = start.AddMinutes(30),
            Status = MeetingRequestStatus.AwaitingSpeaker,
            CreatedAt = SimfClock.Now, RespondedAt = SimfClock.Now,
        };
        db.SpeakerMeetingRequests.Add(req);
        await db.SaveChangesAsync();
        return (req.Id, speaker.Id);
    }
}
