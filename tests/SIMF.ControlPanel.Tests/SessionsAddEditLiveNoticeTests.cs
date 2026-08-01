// FR-702 — behaviour tests for the live notice on the session Add/Edit form.
// The notice replaced a specified GEOGRAPHIC RESTRICTION: the owner reversed that
// on 2026-07-31 ("No restriction, this is only notification and be added to
// session"), so what the form authors is free bilingual copy shown WITH the
// stream — it gates nothing and withholds nothing. These pin the three things
// that make it usable: both boxes render in the LIVE section at the agreed
// 512-character cap, a stored notice loads back into the form and survives a
// save, and emptying a box actually CLEARS the notice (a notice that cannot be
// removed is a defect).
using System.Globalization;
using System.Resources;
using AngleSharp.Dom;
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionsAddEditLiveNoticeTests : CpComponentTestBase
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HallId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const string NoticeEnglish =
        "Broadcast live from the King Abdulaziz hall. Coverage is provided by the RSNF media team.";
    private const string NoticeArabic =
        "يُبث مباشرة من قاعة الملك عبدالعزيز. التغطية بإشراف الإعلام في القوات البحرية الملكية السعودية.";

    private static readonly AdminHallSummary Hall = new(
        HallId, "MAIN", "Main Hall", "القاعة الرئيسية",
        Capacity: 250, Floor: "Ground", IsActive: true, CreatedAt: DateTime.UnixEpoch);

    private static AdminSessionDetail Detail(string? notice, string? noticeArabic) => new(
        Id: SessionId,
        Code: "S-LIVE",
        Title: "Opening plenary",
        TitleArabic: "الجلسة الافتتاحية",
        Description: null,
        DescriptionArabic: null,
        HallId: HallId,
        HallName: "Main Hall",
        HallNameArabic: "القاعة الرئيسية",
        HallCapacity: 250,
        Start: new DateTime(2026, 8, 1, 9, 0, 0),
        End: new DateTime(2026, 8, 1, 10, 30, 0),
        CapacityOverride: null,
        EffectiveCapacity: 250,
        IsActive: true,
        Speakers: Array.Empty<AdminSessionSpeakerEntry>(),
        ThemeIds: Array.Empty<Guid>(),
        CreatedAt: DateTime.UnixEpoch,
        UpdatedAt: null,
        LiveStreamUrl: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        LiveNotice: notice,
        LiveNoticeArabic: noticeArabic);

    /// <summary>The field whose label carries <paramref name="labelKey"/> — the
    /// test localizer renders every key as itself, so the label text IS the resx
    /// key. Locating by label (rather than by index) also proves the two boxes
    /// are labelled from the resx and not from a literal.</summary>
    private static IElement ByLabel(IRenderedComponent<SessionsAddEdit> cut, string labelKey)
    {
        var label = cut.FindAll("label.simf-field__label")
            .First(l => l.TextContent.Trim() == labelKey);
        return cut.Find("#" + label.GetAttribute("for"));
    }

    [Fact]
    public void Add_mode_renders_both_notice_boxes_capped_at_512()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<SessionsAddEdit>(p => p.Add(x => x.IsEdit, false));

        var english = ByLabel(cut, "Admin.Sessions.Field.LiveNotice");
        var arabic = ByLabel(cut, "Admin.Sessions.Field.LiveNoticeArabic");

        // 512 is the agreed cap and must match the FluentValidation and EF lengths.
        Assert.Equal("512", english.GetAttribute("maxlength"));
        Assert.Equal("512", arabic.GetAttribute("maxlength"));

        // The helper text is what tells the admin the notice restricts nothing.
        Assert.Contains("Admin.Sessions.Field.LiveNoticeHint", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hint_tells_the_admin_the_notice_restricts_nobody()
    {
        // The markup assertion above can only prove the Helper attribute is wired:
        // the test localizer echoes every key, so it passes the moment
        // Helper="@L[...]" exists and would still pass if the WORDING were reverted
        // to the old geo-restriction copy. FR-702 was a restriction until the owner
        // reversed it on 2026-07-31, so the wording is the thing most likely to
        // drift back. Assert the resource VALUE itself.
        var hint = new ResourceManager(
                "SIMF.ControlPanel.Resources.Strings",
                typeof(Strings).Assembly)
            .GetString("Admin.Sessions.Field.LiveNoticeHint", CultureInfo.InvariantCulture);

        Assert.False(string.IsNullOrWhiteSpace(hint));
        Assert.Contains("information only", hint, StringComparison.OrdinalIgnoreCase);

        // The exact phrasing may be edited; a re-introduced RESTRICTION may not.
        Assert.DoesNotContain("only within", hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Riyadh region", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Edit_mode_loads_the_stored_notice_into_both_boxes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<SessionsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, Detail(NoticeEnglish, NoticeArabic)));

        Assert.Equal(NoticeEnglish,
            ByLabel(cut, "Admin.Sessions.Field.LiveNotice").GetAttribute("value"));
        Assert.Equal(NoticeArabic,
            ByLabel(cut, "Admin.Sessions.Field.LiveNoticeArabic").GetAttribute("value"));
    }

    [Fact]
    public void Add_mode_posts_the_typed_notice()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminHallSummary>>>(
            "simfAccount.postJson",
            call => call.Arguments[0] is string url
                && url.EndsWith("/halls/list", StringComparison.Ordinal))
            .SetResult(ApiResult<GridPage<AdminHallSummary>>.Ok(
                GridPage<AdminHallSummary>.Of([Hall], 1, 0, 500)));
        var handler = JSInterop.Setup<ApiResult<AdminSessionDetail>>(
            "simfAccount.postJson",
            call => call.Arguments[0] is string url
                && url == "/account/api/admin/sessions")
            .SetResult(ApiResult<AdminSessionDetail>.Ok(Detail(NoticeEnglish, NoticeArabic)));

        var cut = RenderComponent<SessionsAddEdit>(p => p.Add(x => x.IsEdit, false));
        cut.WaitForElement($"option[value=\"{HallId}\"]");

        ByLabel(cut, "Admin.Sessions.Field.Code").Change("S-LIVE");
        ByLabel(cut, "Admin.Sessions.Field.Title").Change("Opening plenary");
        ByLabel(cut, "Admin.Sessions.Field.TitleArabic").Change("الجلسة الافتتاحية");
        ByLabel(cut, "Admin.Sessions.Field.Hall").Change(HallId.ToString());
        // Type "Event" (2) — the only type the form lets through without a speaker.
        ByLabel(cut, "Admin.Sessions.Field.Type *").Change("2");
        ByLabel(cut, "Admin.Sessions.Field.Start").Change("2026-08-01T09:00");
        ByLabel(cut, "Admin.Sessions.Field.End").Change("2026-08-01T10:30");
        // The notice boxes are textareas — they commit on input, not on change.
        ByLabel(cut, "Admin.Sessions.Field.LiveNotice").Input(NoticeEnglish);
        ByLabel(cut, "Admin.Sessions.Field.LiveNoticeArabic").Input(NoticeArabic);

        cut.Find("form").Submit();

        var body = (AdminCreateSessionRequest)handler.Invocations.Single().Arguments[1]!;
        Assert.Equal(NoticeEnglish, body.LiveNotice);
        Assert.Equal(NoticeArabic, body.LiveNoticeArabic);
    }

    [Fact]
    public void Edit_mode_puts_the_edited_notice()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail(NoticeEnglish, NoticeArabic);
        var handler = JSInterop.Setup<ApiResult<AdminSessionDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminSessionDetail>.Ok(row));

        var cut = RenderComponent<SessionsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        ByLabel(cut, "Admin.Sessions.Field.LiveNotice")
            .Input("Sign-language interpretation runs alongside this session.");

        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/sessions/{row.Id}", (string)put.Arguments[0]!);
        var body = (AdminUpdateSessionRequest)put.Arguments[1]!;
        Assert.Equal("Sign-language interpretation runs alongside this session.", body.LiveNotice);
        // The untouched half of the pair still round-trips.
        Assert.Equal(NoticeArabic, body.LiveNoticeArabic);
    }

    [Fact]
    public void Emptying_both_boxes_clears_the_notice()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = Detail(NoticeEnglish, NoticeArabic);
        var handler = JSInterop.Setup<ApiResult<AdminSessionDetail>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminSessionDetail>.Ok(row));

        var cut = RenderComponent<SessionsAddEdit>(p => p
            .Add(x => x.IsEdit, true)
            .Add(x => x.Initial, row));

        ByLabel(cut, "Admin.Sessions.Field.LiveNotice").Input(string.Empty);
        ByLabel(cut, "Admin.Sessions.Field.LiveNoticeArabic").Input("   ");

        cut.Find("form").Submit();

        // Both halves must reach the API as null, otherwise a notice an admin has
        // taken down keeps showing beside the stream.
        var body = (AdminUpdateSessionRequest)handler.Invocations.Single().Arguments[1]!;
        Assert.Null(body.LiveNotice);
        Assert.Null(body.LiveNoticeArabic);
    }
}
