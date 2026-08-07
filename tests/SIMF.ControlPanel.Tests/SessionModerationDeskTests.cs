// DEF-MOD-001 (r2) — the CP desk reads the same moderator list as the app, and
// since the answered mark became a persisted status that list now carries
// Answered rows. The state cell only tested IsHidden then IsPushed, so a
// not-yet-pushed answered question rendered as "Queued" — the CP told the admin
// the opposite of what the moderator had just recorded. These tests pin the cell.
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionModerationDeskTests : CpComponentTestBase
{
    // The PassThroughStringLocalizer echoes the resx key.
    private const string AnsweredLabel = "Admin.SessionModeration.State.Answered";
    private const string QueuedLabel = "Admin.SessionModeration.State.Queued";
    private const string PushedLabel = "Admin.SessionModeration.Pushed";

    private static readonly Guid SessionId = Guid.NewGuid();

    private static SessionQuestionModeratorRow Row(
        QuestionStatus status, bool isPushed = false) => new(
        Guid.NewGuid(),
        SessionId,
        Guid.NewGuid(),
        "Sara Al-Otaibi",
        null,
        "What is the refit schedule?",
        SessionQuestionRecipient.Speaker,
        0,
        status == QuestionStatus.Hidden,
        isPushed,
        isPushed ? DateTime.UnixEpoch : null,
        DateTime.UnixEpoch,
        QuestionPhase.Live,
        status);

    private void Arrange(params SessionQuestionModeratorRow[] rows)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(new CpPreferences(JSInterop.JSRuntime));
        JSInterop.Setup<ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>>(
                invocation => invocation.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<IReadOnlyList<SessionQuestionModeratorRow>>.Ok(rows));
    }

    [Fact]
    public void An_answered_question_is_labelled_answered_not_queued()
    {
        Arrange(Row(QuestionStatus.Answered));

        var cut = RenderComponent<SessionModerationDesk>(
            parameters => parameters.Add(desk => desk.SessionId, SessionId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(AnsweredLabel, cut.Markup);
            Assert.DoesNotContain(QueuedLabel, cut.Markup);
        });
    }

    [Fact]
    public void An_answered_question_that_was_pushed_still_reads_answered()
    {
        // Answered is the terminal desk state — a question is normally pushed on
        // stage first and answered after, so it must not fall back to "Pushed".
        Arrange(Row(QuestionStatus.Answered, isPushed: true));

        var cut = RenderComponent<SessionModerationDesk>(
            parameters => parameters.Add(desk => desk.SessionId, SessionId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(AnsweredLabel, cut.Markup);
            Assert.DoesNotContain(PushedLabel, cut.Markup);
        });
    }

    [Fact]
    public void A_plain_approved_question_is_still_queued()
    {
        Arrange(Row(QuestionStatus.Approved));

        var cut = RenderComponent<SessionModerationDesk>(
            parameters => parameters.Add(desk => desk.SessionId, SessionId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(QueuedLabel, cut.Markup);
            Assert.DoesNotContain(AnsweredLabel, cut.Markup);
        });
    }
}
