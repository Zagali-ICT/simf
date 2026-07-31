// DEF-MOD-005 — the assign-moderator dialog used to take the session id and the
// user id as free-text GUIDs, so any account could be handed a moderation desk by
// a typo and there was no way to find the right person. These tests pin the two
// pickers that replaced them: the options come from the gated lookup, and the
// submit posts the PICKED ids (never parsed text).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionModeratorsListTests : CpComponentTestBase
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ModeratorId = Guid.NewGuid();

    private static SessionModeratorAssignOptions Options() => new(
        new List<SessionModeratorSessionOption>
        {
            new(SessionId, "SES-01", "Opening", "الافتتاح"),
        },
        new List<SessionModeratorCandidate>
        {
            new(ModeratorId, "Reem Al-Harbi", "reem@simf.test", "Moderator", "منسّق"),
        });

    private static AdminSessionModeratorRow Row() => new(
        SessionId, "SES-01", "Opening", "الافتتاح",
        ModeratorId, "Reem Al-Harbi", "reem@simf.test",
        Guid.NewGuid(), "Admin", DateTime.UnixEpoch);

    [Fact]
    public void The_assign_dialog_offers_pickers_not_raw_GUID_boxes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminSessionModeratorRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminSessionModeratorRow>>.Ok(
                GridPage<AdminSessionModeratorRow>.Of(
                    Array.Empty<AdminSessionModeratorRow>(), 0, 0, 20)));
        JSInterop.Setup<ApiResult<SessionModeratorAssignOptions>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<SessionModeratorAssignOptions>.Ok(Options()));

        var cut = RenderComponent<SessionModeratorsList>();
        cut.Find("button[title='Grid.Add']").Click();

        // Two selects (session + moderator), populated from the lookup …
        var selects = cut.FindAll("select.simf-field__select");
        Assert.Equal(2, selects.Count);
        Assert.Contains("SES-01 — Opening", cut.Markup);
        Assert.Contains("Reem Al-Harbi", cut.Markup);
        // … and the old free-text GUID fields are gone.
        Assert.DoesNotContain("Admin.SessionModerators.Field.SessionId", cut.Markup);
        Assert.DoesNotContain("Admin.SessionModerators.Field.UserId", cut.Markup);
    }

    [Fact]
    public void Assign_posts_the_picked_session_and_moderator_ids()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminSessionModeratorRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminSessionModeratorRow>>.Ok(
                GridPage<AdminSessionModeratorRow>.Of(
                    Array.Empty<AdminSessionModeratorRow>(), 0, 0, 20)));
        JSInterop.Setup<ApiResult<SessionModeratorAssignOptions>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<SessionModeratorAssignOptions>.Ok(Options()));
        var assign = JSInterop.Setup<ApiResult<AdminSessionModeratorRow>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminSessionModeratorRow>.Ok(Row()));

        var cut = RenderComponent<SessionModeratorsList>();
        cut.Find("button[title='Grid.Add']").Click();

        cut.FindAll("select.simf-field__select")[0].Change(SessionId.ToString());
        cut.FindAll("select.simf-field__select")[1].Change(ModeratorId.ToString());
        cut.Find("button.simf-button--primary").Click();

        var posted = assign.Invocations.Single();
        Assert.Equal("/account/api/admin/session-moderators", (string)posted.Arguments[0]!);
        var body = (AssignSessionModeratorRequest)posted.Arguments[1]!;
        Assert.Equal(SessionId, body.SessionId);
        Assert.Equal(ModeratorId, body.UserId);
    }

    [Fact]
    public void An_empty_candidate_list_says_so_and_disables_the_moderator_picker()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminSessionModeratorRow>>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<GridPage<AdminSessionModeratorRow>>.Ok(
                GridPage<AdminSessionModeratorRow>.Of(
                    Array.Empty<AdminSessionModeratorRow>(), 0, 0, 20)));
        JSInterop.Setup<ApiResult<SessionModeratorAssignOptions>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<SessionModeratorAssignOptions>.Ok(
                new SessionModeratorAssignOptions(
                    new List<SessionModeratorSessionOption>
                    {
                        new(SessionId, "SES-01", "Opening", "الافتتاح"),
                    },
                    Array.Empty<SessionModeratorCandidate>())));

        var cut = RenderComponent<SessionModeratorsList>();
        cut.Find("button[title='Grid.Add']").Click();

        Assert.Contains("Admin.SessionModerators.NoCandidates", cut.Markup);
        Assert.True(cut.FindAll("select.simf-field__select")[1].HasAttribute("disabled"));
    }
}
