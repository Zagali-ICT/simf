// Tests: R4 (bi-meeting rules, D-767) — delegation confirm-by-link action tokens.
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Halls.Add(hall);

        var start = new DateTimeOffset(2031, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var req = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestedByUserId = Guid.NewGuid(),
            RequestingCountryId = requesting.Id,
            TargetCountryId = target.Id,
            AttendeeCount = 3,
            Subject = "Naval cooperation",
            HallId = hall.Id, SlotStartUtc = start, SlotEndUtc = start.AddMinutes(30),
            Status = MeetingRequestStatus.AwaitingSpeaker,
            CreatedAt = DateTimeOffset.UtcNow, RespondedAt = DateTimeOffset.UtcNow,
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
                IsActive = true, IsInvited = true, CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Countries.Add(country);
            await db.SaveChangesAsync();
        }
        return country;
    }
}
