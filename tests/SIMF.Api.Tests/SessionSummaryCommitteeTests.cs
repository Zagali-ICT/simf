// P4.1b — D-238 (Completion Programme §6.4.1): the Scientific-Committee session-
// summary / محضر desk. Covers AI-draft (Echo provider), hand-written save,
// publish → public read → unpublish, the desk list, length validation, the
// publish-without-summary 404, and the non-committee forbidden case.
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
using SIMF.Contracts.Programme;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Domain.Programme;
using SIMF.Domain.SessionQuestions;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SessionSummaryCommitteeTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionSummaryCommitteeTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Generate_drafts_a_summary_through_the_ai_seam()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var detail = await GenerateAsync(sessionId, admin);
        // The seeded prompt routes to the Echo provider — a deterministic draft.
        Assert.NotNull(detail.AiModel);
        // The prompt produces Arabic, so the draft lands in the Arabic column
        // only; the English column stays empty for the Committee to fill.
        Assert.False(string.IsNullOrWhiteSpace(detail.FullTextArabic));
        Assert.True(string.IsNullOrEmpty(detail.FullText));
        Assert.False(detail.IsPublished);
    }

    [Fact]
    public async Task Generate_feeds_the_session_subtitle_transcript_into_the_ai_draft()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A session whose live subtitle/transcript carries a distinctive phrase.
        var sessionId = await SeedSessionAsync(
            liveCaptionsArabic: "تعاون الغوّاصات وأمن الملاحة البحرية");

        var detail = await GenerateAsync(sessionId, admin);

        // The Echo provider echoes the substituted user prompt, which now carries
        // the session's transcript — proving LiveCaptions flows into the AI input.
        Assert.Contains("تعاون الغوّاصات", detail.FullTextArabic!);
    }

    // -- Slice D (2026-07-19): AI transparency. The pristine AI-draft snapshot is
    // captured at generation and must survive every Committee edit; the raw subtitle
    // the AI drafted from is surfaced (read-only) on the editor read. Both are
    // CP-internal and never projected onto a public contract.

    [Fact]
    public async Task Generate_captures_the_pristine_ai_draft_snapshot()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(
            liveCaptionsArabic: "الردع البحري وحماية الممرات");

        var detail = await GenerateAsync(sessionId, admin);

        // The snapshot is captured and, on a fresh generate, equals the editable
        // Arabic draft the AI just produced.
        Assert.False(string.IsNullOrWhiteSpace(detail.AiDraftFullTextArabic));
        Assert.Equal(detail.FullTextArabic, detail.AiDraftFullTextArabic);
        Assert.Contains("الردع البحري", detail.AiDraftFullTextArabic!);
        Assert.NotNull(detail.AiDraftGeneratedAt);
    }

    [Fact]
    public async Task Editing_the_summary_preserves_the_pristine_ai_draft()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(
            liveCaptionsArabic: "الردع البحري وحماية الممرات");
        var generated = await GenerateAsync(sessionId, admin);
        var pristine = generated.AiDraftFullTextArabic;
        var generatedAt = generated.AiDraftGeneratedAt;

        // The Committee edits the working Arabic full-text away from the AI draft.
        var edited = await SaveAsync(sessionId,
            new SaveSessionSummaryRequest { FullTextArabic = "محضر حرّره الفريق العلمي يدويًا." }, admin);

        // The editable copy took the edit; the pristine AI draft is untouched.
        Assert.Equal("محضر حرّره الفريق العلمي يدويًا.", edited.FullTextArabic);
        Assert.Equal(pristine, edited.AiDraftFullTextArabic);
        Assert.NotEqual(edited.FullTextArabic, edited.AiDraftFullTextArabic);
        Assert.Equal(generatedAt, edited.AiDraftGeneratedAt);
    }

    [Fact]
    public async Task Regenerating_refreshes_the_pristine_ai_draft_snapshot()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(liveCaptionsArabic: "الموضوع الأول");
        var first = await GenerateAsync(sessionId, admin);
        Assert.Contains("الموضوع الأول", first.AiDraftFullTextArabic!);

        // The transcript is corrected, then the draft is re-generated: the snapshot
        // must track the LATEST AI output, not the stale first draft.
        await SetSessionCaptionsDirectAsync(sessionId, liveCaptionsArabic: "الموضوع الثاني");
        var second = await GenerateAsync(sessionId, admin);

        Assert.Contains("الموضوع الثاني", second.AiDraftFullTextArabic!);
        Assert.DoesNotContain("الموضوع الأول", second.AiDraftFullTextArabic!);
    }

    [Fact]
    public async Task The_editor_read_surfaces_the_raw_subtitle_source()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(
            liveCaptions: "Maritime deterrence and sea-lane protection.",
            liveCaptionsArabic: "الردع البحري وحماية الممرات");
        // A summary must exist for the editor read to return a detail.
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var detail = await GetDetailAsync(sessionId, admin);

        // The editor now sees the raw subtitle the AI drafts from, both languages.
        Assert.Equal("Maritime deterrence and sea-lane protection.", detail.Subtitle);
        Assert.Equal("الردع البحري وحماية الممرات", detail.SubtitleArabic);
    }

    [Fact]
    public async Task Save_creates_a_hand_written_draft_with_no_ai_model()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var detail = await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            KeyPoints = "Point one\nPoint two",
            Recommendations = "Strengthen cooperation.",
            Speakers = "Speaker A · Speaker B",
            FullText = "The session covered maritime supply-chain security.",
        }, admin);

        Assert.Null(detail.AiModel);
        Assert.Equal("Point one\nPoint two", detail.KeyPoints);
        Assert.False(detail.IsPublished);
    }

    [Fact]
    public async Task Publish_exposes_the_public_read_then_unpublish_hides_it()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // S-6 — a summary can only be published once the session has started; the
        // default seed uses a past start (started).
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullText = "Minutes.", FullTextArabic = "محضر.",
        }, admin);

        // Before publish: the public read is 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);

        // The scientific team must review + approve before publish is allowed.
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        var published = await _client.GetAsync(PublicUrl(sessionId));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var body = (await published.Content
            .ReadFromJsonAsync<ApiResult<PublicSessionSummary>>())!.Data!;
        Assert.Equal("محضر.", body.FullTextArabic);

        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/unpublish", new { }, admin);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    [Fact]
    public async Task The_desk_list_reflects_the_summary_state()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // S-6 — publish requires a started session (the default seed's past start).
        var sessionId = await SeedSessionAsync();
        await GenerateAsync(sessionId, admin);
        // A18 — the shipped Echo stub only echoes the prompt, and its output can
        // no longer be approved/published as-is; the Committee replaces it with
        // the real minutes first (AiModel stays set, so the row is still AI-drafted).
        await SaveAsync(sessionId,
            new SaveSessionSummaryRequest { FullTextArabic = "محضر حرّره الفريق." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        var response = await GetAuthAsync("/api/v1/admin/session-summaries", admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>())!.Data!;
        var row = Assert.Single(rows, r => r.SessionId == sessionId);
        Assert.True(row.HasSummary);
        Assert.True(row.GeneratedByAi);
        Assert.True(row.IsPublished);
    }

    [Fact]
    public async Task An_oversized_section_is_rejected_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}",
            new SaveSessionSummaryRequest { FullText = new string('x', 8001) }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Item #35 (2026-07-20): the optional team summary-video URL ------------

    [Fact]
    public async Task Save_round_trips_the_summary_video_url()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        // A valid YouTube watch link (an extractable 11-char id) is persisted...
        var saved = await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "محضر.",
            SummaryVideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        }, admin);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", saved.SummaryVideoUrl);

        // ...and survives a fresh editor read.
        var reread = await GetDetailAsync(sessionId, admin);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", reread.SummaryVideoUrl);
    }

    [Fact]
    public async Task An_invalid_summary_video_url_is_rejected_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        // Not a YouTube link and not a direct .m3u8/.mp4 stream → rejected by the
        // shared LiveStreamUrlPolicy (same rule as the session's live feed).
        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}",
            new SaveSessionSummaryRequest
            {
                FullTextArabic = "محضر.",
                SummaryVideoUrl = "https://vimeo.com/123456789",
            }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task A_null_summary_video_url_is_allowed()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        // Omitting the URL (the common case) is fine — the summary saves with no
        // second video, and the detail reports it as null.
        var saved = await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "محضر.",
            SummaryVideoUrl = null,
        }, admin);
        Assert.Null(saved.SummaryVideoUrl);

        // A blank string is normalized to null too (cleared, not an empty value).
        var cleared = await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "محضر.",
            SummaryVideoUrl = "   ",
        }, admin);
        Assert.Null(cleared.SummaryVideoUrl);
    }

    [Fact]
    public async Task Publishing_a_session_with_no_summary_is_404()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // S-6 (owner) — a محضر may only be PUBLISHED once the session has STARTED.
    [Fact]
    public async Task PublishAsync_BeforeSessionStarts_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A future session — it has not started yet.
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddDays(1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task PublishAsync_AfterSessionStarts_Succeeds()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A started session (past start).
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.True(detail.IsPublished);
    }

    // Owner 2026-07-19 — a محضر can only be PUBLISHED after the scientific team has
    // reviewed and APPROVED it; publishing a merely-drafted (unapproved) summary is
    // rejected so the app never shows unreviewed minutes.
    [Fact]
    public async Task PublishAsync_WithoutApproval_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        // A started session (the S-6 clock gate passes); the summary is saved but
        // never submitted for review or approved.
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);

        // Nothing leaked — the public read stays 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    // Owner 2026-07-19 — editing a PUBLISHED summary invalidates its approval, so it
    // must also come offline; the app can never keep serving edited, unapproved text.
    [Fact]
    public async Task Editing_a_published_summary_takes_it_offline_until_reapproved()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        // The app sees the approved, published summary.
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);

        // Editing it clears the approval — and therefore unpublishes it.
        await SaveAsync(sessionId,
            new SaveSessionSummaryRequest { FullTextArabic = "محضر مُحدَّث." }, admin);

        var rows = (await (await GetAuthAsync("/api/v1/admin/session-summaries", admin)).Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>())!.Data!;
        var row = Assert.Single(rows, r => r.SessionId == sessionId);
        Assert.False(row.IsPublished);
        Assert.False(row.IsApproved);
        // The app no longer serves the edited, unapproved text.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    // -- A18 (2026-07-26): the shipped AI provider is the offline Echo stub, which
    // only echoes the prompt back. Its output must be impossible to mistake for
    // real minutes and impossible to sign off / ship to the app.

    [Fact]
    public async Task A18_the_stub_draft_is_marked_so_a_reviewer_cannot_mistake_it_for_minutes()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(liveCaptionsArabic: "الردع البحري");

        var detail = await GenerateAsync(sessionId, admin);

        // The draft opens with the machine-checkable sentinel and says, in both
        // languages, that it is not real AI output.
        Assert.StartsWith("[AI-STUB-DO-NOT-PUBLISH]", detail.FullTextArabic!);
        Assert.Contains("NOT REAL AI OUTPUT", detail.FullTextArabic!);
        Assert.Contains("ليست مخرجات ذكاء اصطناعي حقيقية", detail.FullTextArabic!);
        // The prompt content still rides along, so the existing transparency panel
        // (and the transcript-flows-into-the-draft contract) is unchanged.
        Assert.Contains("الردع البحري", detail.FullTextArabic!);
    }

    [Fact]
    public async Task A18_ApproveAsync_WithStubDraft_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await GenerateAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task A18_PublishAsync_WithStubTextPastedBack_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        // A clean summary is approved...
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        // ...then the stub text is pasted into a DIFFERENT column and re-approved.
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "محضر.",
            KeyPoints = "[AI-STUB-DO-NOT-PUBLISH] NOT REAL AI OUTPUT — echoed prompt.",
        }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        var approve = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, approve.StatusCode);

        // Nothing reached the app.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    [Fact]
    public async Task A18_a_stub_draft_replaced_by_real_minutes_publishes_normally()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await GenerateAsync(sessionId, admin);
        // The Committee does its job: the placeholder is replaced.
        await SaveAsync(sessionId,
            new SaveSessionSummaryRequest { FullTextArabic = "محضر حرّره الفريق العلمي." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    // -- A18-r2 (2026-07-27): the guard has to cover the rows that are ALREADY on
    // the QA / production databases. Every draft the stub produced before the
    // sentinel existed opens with the bare "[echo:model] " prefix; those rows must
    // be just as unapprovable and unpublishable as a freshly stamped one.

    [Fact]
    public async Task A18_ApproveAsync_WithLegacyEchoPrefixedDraft_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        // Exactly what Generate stored before this guard shipped: the stub's own
        // "[echo:echo] " prefix followed by the echoed prompt.
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "[echo:echo] اكتب محضراً لجلسة: الردع البحري",
        }, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);

        var approve = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);

        Assert.Equal(HttpStatusCode.BadRequest, approve.StatusCode);
        var body = (await approve.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task A18_PublishAsync_WithLegacyEchoPrefixedDraftAlreadyApproved_ReturnsBadRequest()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        // The pre-existing case: a stub draft that was approved BEFORE the guard
        // shipped, sitting one Publish click away from every visitor.
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "[echo] اكتب محضراً لجلسة: الردع البحري",
        }, admin);
        await SetSummaryApprovedDirectAsync(sessionId);

        var publish = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        var body = (await publish.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SessionSummaryInvalid, body.Error!.Code);
        // The echoed prompt never reaches the app.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    [Fact]
    public async Task A18_real_minutes_that_merely_mention_echo_still_publish()
    {
        // The legacy sweep is LEADING-prefix only, so genuine minutes that happen
        // to quote the token are not collateral damage.
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest
        {
            FullTextArabic = "ناقش المتحدثون تقنية [echo] للسونار في المياه الضحلة.",
        }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var publish = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    // -- A19 (2026-07-26): a save that changes nothing must not silently retract a
    // live محضر. A save that DOES change the content still does (the approval was
    // of the old text) — the CP warns about that consequence before it happens.

    [Fact]
    public async Task A19_saving_a_published_summary_unchanged_keeps_it_published()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        var request = new SaveSessionSummaryRequest
        {
            KeyPoints = "Point one",
            FullText = "Minutes.",
            FullTextArabic = "محضر.",
            SummaryVideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        };
        await SaveAsync(sessionId, request, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        // Re-opening the editor and pressing Save without touching a field.
        var resaved = await SaveAsync(sessionId, request, admin);

        Assert.True(resaved.IsPublished);
        Assert.True(resaved.IsApproved);
        // The app still serves it — no silent outage from a no-op save.
        Assert.Equal(HttpStatusCode.OK,
            (await _client.GetAsync(PublicUrl(sessionId))).StatusCode);
    }

    [Fact]
    public async Task A19_regenerating_the_same_draft_keeps_the_summary_published()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        var generated = await GenerateAsync(sessionId, admin);
        // Stamp it approved + published directly (the A18 gate deliberately blocks
        // approving stub text through the API; this test is about A19's reset rule,
        // not about that gate), leaving the stored Arabic text as the draft.
        await SetSummaryApprovedAndPublishedDirectAsync(sessionId);

        // A second draft from the same session inputs is byte-identical, so nothing
        // the app serves changed and the محضر must stay online.
        var again = await GenerateAsync(sessionId, admin);

        Assert.Equal(generated.FullTextArabic, again.FullTextArabic);
        Assert.True(again.IsPublished);
        Assert.True(again.IsApproved);

        // The transcript is corrected — now the draft really changes, and the
        // D-472 retraction still fires.
        await SetSessionCaptionsDirectAsync(sessionId, liveCaptionsArabic: "نص مختلف");
        var redrafted = await GenerateAsync(sessionId, admin);
        Assert.NotEqual(again.FullTextArabic, redrafted.FullTextArabic);
        Assert.False(redrafted.IsPublished);
        Assert.False(redrafted.IsApproved);
    }

    [Fact]
    public async Task UnpublishAsync_WhileScheduled_StillAllowed()
    {
        // Unpublish only retracts, so it is allowed regardless of the clock — even
        // after the session is rescheduled back into the future.
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync(start: SimfClock.Now.AddHours(-1));
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/publish", new { }, admin);

        // Reschedule the session into the future (it "hasn't started" again).
        await SetSessionStartDirectAsync(sessionId, SimfClock.Now.AddDays(1));

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/unpublish", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.False(detail.IsPublished);
    }

    [Fact]
    public async Task A_non_committee_account_is_forbidden_from_the_desk()
    {
        var visitor = await SeedApprovedVisitorAsync();

        var response = await GetAuthAsync("/api/v1/admin/session-summaries", visitor);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- D-472 (#9): the team review/approval workflow + host/moderator read ---

    [Fact]
    public async Task Submit_then_approve_marks_the_summary_ready_and_the_desk_reflects_it()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var submitted = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var inReview = (await submitted.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.True(inReview.IsInReview);
        Assert.False(inReview.IsApproved);

        var approved = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var detail = (await approved.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.True(detail.IsApproved);
        Assert.False(detail.IsInReview);
        Assert.NotNull(detail.ApprovedAt);

        var rows = await ListAsync(admin);
        var row = Assert.Single(rows, r => r.SessionId == sessionId);
        Assert.True(row.IsApproved);
    }

    [Fact]
    public async Task Approving_without_submitting_is_400()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);

        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_submitted_summary_returns_it_to_draft()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, admin);

        // Any edit invalidates the review → back to Draft.
        var detail = await SaveAsync(
            sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر معدّل." }, admin);
        Assert.False(detail.IsInReview);
        Assert.False(detail.IsApproved);
    }

    [Fact]
    public async Task Return_to_draft_clears_an_approval()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var returned = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/return-to-draft", new { }, admin);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        var detail = (await returned.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
        Assert.False(detail.IsApproved);
        Assert.False(detail.IsInReview);
    }

    [Fact]
    public async Task A_session_moderator_reads_the_approved_summary_and_404_before_approval()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (modToken, modUserId) = await CreateApprovedVisitorAsync();
        await SeedSessionModeratorAsync(sessionId, modUserId);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر معتمد." }, admin);

        // Authorized, but no approved summary yet → 404.
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAuthAsync(HostUrl(sessionId), modToken)).StatusCode);

        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), modToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var body = (await read.Content
            .ReadFromJsonAsync<ApiResult<HostSessionSummary>>())!.Data!;
        Assert.Equal("محضر معتمد.", body.FullTextArabic);
    }

    [Fact]
    public async Task The_session_host_reads_the_approved_summary()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (hostToken, hostUserId) = await CreateApprovedVisitorAsync();
        await SeedSessionHostAsync(sessionId, hostUserId);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), hostToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_host_is_forbidden_from_the_approved_read()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (hostToken, hostUserId) = await CreateApprovedVisitorAsync();
        // The host's speaker row is soft-deleted → no longer a host.
        await SeedSessionHostAsync(sessionId, hostUserId, speakerActive: false);
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), hostToken);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    [Fact]
    public async Task A_non_moderator_non_host_visitor_is_forbidden_from_the_approved_read()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var sessionId = await SeedSessionAsync();
        var (token, _) = await CreateApprovedVisitorAsync();
        await SaveAsync(sessionId, new SaveSessionSummaryRequest { FullTextArabic = "محضر." }, admin);
        await SubmitForReviewAndApproveAsync(sessionId, admin);

        var read = await GetAuthAsync(HostUrl(sessionId), token);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

    private static string PublicUrl(Guid sessionId) =>
        $"/api/v1/app/programme/sessions/{sessionId}/summary";

    private static string HostUrl(Guid sessionId) =>
        $"/api/v1/app/programme/sessions/{sessionId}/summary/approved";

    private async Task<IReadOnlyList<AdminSessionSummaryRow>> ListAsync(string token)
    {
        var response = await GetAuthAsync("/api/v1/admin/session-summaries", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<IReadOnlyList<AdminSessionSummaryRow>>>())!.Data!;
    }

    private async Task<(string Token, Guid UserId)> CreateApprovedVisitorAsync()
    {
        var email = $"sum-reader-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Summary Reader",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        return (token, userId);
    }

    private async Task SeedSessionModeratorAsync(Guid sessionId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.SessionModerators.Add(new SessionModerator
        {
            SessionId = sessionId,
            UserId = userId,
            AssignedByUserId = Guid.NewGuid(),
            AssignedAt = SimfClock.Now,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedSessionHostAsync(Guid sessionId, Guid userId, bool speakerActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Host User",
            NameArabic = "المحاور",
            CreatedAt = SimfClock.Now,
        };
        db.UserProfiles.Add(profile);
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = "SPK-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Host User",
            NameArabic = "المحاور",
            UserProfileId = profile.Id,
            IsActive = speakerActive,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        db.SessionSpeakers.Add(new SessionSpeaker
        {
            SessionId = sessionId,
            SpeakerId = speaker.Id,
            DisplayOrder = 0,
            Role = SessionSpeakerRole.Host,
        });
        await db.SaveChangesAsync();
    }

    private async Task<AdminSessionSummaryDetail> GenerateAsync(Guid sessionId, string token)
    {
        var response = await PostAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}/generate", new { }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
    }

    private async Task<AdminSessionSummaryDetail> SaveAsync(
        Guid sessionId, SaveSessionSummaryRequest request, string token)
    {
        var response = await PutAuthAsync(
            $"/api/v1/admin/session-summaries/{sessionId}", request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
    }

    // Slice D — the editor read (OpenEditor's GET) that now also carries the raw
    // subtitle + the pristine AI-draft snapshot.
    private async Task<AdminSessionSummaryDetail> GetDetailAsync(Guid sessionId, string token)
    {
        var response = await GetAuthAsync($"/api/v1/admin/session-summaries/{sessionId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionSummaryDetail>>())!.Data!;
    }

    // Owner 2026-07-19 — the summary desk's common setup: submit a draft for review
    // then approve it (the two steps that unblock Publish). Collapses the pair that
    // otherwise repeats across the publish / approved-read tests.
    private async Task SubmitForReviewAndApproveAsync(Guid sessionId, string token)
    {
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/submit-review", new { }, token);
        await PutAuthAsync($"/api/v1/admin/session-summaries/{sessionId}/approve", new { }, token);
    }

    private async Task<Guid> SeedSessionAsync(
        string? liveCaptions = null, string? liveCaptionsArabic = null,
        DateTime? start = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Summary Hall", NameArabic = "قاعة الملخص",
            Capacity = 100, IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);
        // S-6 — the publish gate is clock-based: default to a STARTED session (past
        // start) so publish is allowed; the "before start" tests pass a future start.
        var startValue = start ?? SimfClock.Now.AddMinutes(-90);
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Maritime Supply-Chain Security", TitleArabic = "أمن سلاسل الإمداد البحرية",
            HallId = hall.Id,
            Start = startValue,
            End = startValue.AddHours(1),
            LiveCaptions = liveCaptions,
            LiveCaptionsArabic = liveCaptionsArabic,
            IsActive = true, CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private async Task SetSessionStartDirectAsync(Guid sessionId, DateTime start)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await db.Sessions.FindAsync(sessionId);
        session!.Start = start;
        session.End = start.AddHours(1);
        await db.SaveChangesAsync();
    }

    // A19 — stamp a summary approved + published straight in the DB. The API path
    // is deliberately blocked for stub text (A18), and this test needs a LIVE
    // summary whose stored content is exactly what a re-generate will produce.
    private async Task SetSummaryApprovedAndPublishedDirectAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var summary = await db.SessionSummaries
            .SingleAsync(s => s.SessionId == sessionId && s.IsActive);
        var now = SimfClock.Now;
        var actor = Guid.NewGuid();
        summary.ReviewSubmittedAt = now;
        summary.ReviewSubmittedByUserId = actor;
        summary.ApprovedAt = now;
        summary.ApprovedByUserId = actor;
        summary.PublishedAt = now;
        summary.PublishedByUserId = actor;
        await db.SaveChangesAsync();
    }

    // A18-r2 — stamp a summary approved but NOT published, straight in the DB. That
    // is the state a legacy stub row is really in on a QA / production database: it
    // was approved before the guard existed, so only the publish gate can still stop
    // it. The API approve path would refuse the same content first.
    private async Task SetSummaryApprovedDirectAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var summary = await db.SessionSummaries
            .SingleAsync(s => s.SessionId == sessionId && s.IsActive);
        var now = SimfClock.Now;
        var actor = Guid.NewGuid();
        summary.ReviewSubmittedAt = now;
        summary.ReviewSubmittedByUserId = actor;
        summary.ApprovedAt = now;
        summary.ApprovedByUserId = actor;
        await db.SaveChangesAsync();
    }

    // Slice D — rewrite the session's transcript directly, so a re-generate drafts
    // from a different source (proving the snapshot refreshes to the latest output).
    private async Task SetSessionCaptionsDirectAsync(
        Guid sessionId, string? liveCaptions = null, string? liveCaptionsArabic = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var session = await db.Sessions.FindAsync(sessionId);
        session!.LiveCaptions = liveCaptions;
        session.LiveCaptionsArabic = liveCaptionsArabic;
        await db.SaveChangesAsync();
    }

    private async Task<string> SeedApprovedVisitorAsync()
    {
        var email = $"sum-visitor-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Summary Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
        }
        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"sum-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Summary Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }
        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
