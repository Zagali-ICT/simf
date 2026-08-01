// Tests: SIMF.Api.Tests/AssistanceContextBuilderTests.cs
using System.Globalization;
using System.Text;
using SIMF.Application.Ai.Abstractions;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Faq.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Contracts.Ai;

namespace SIMF.Infrastructure.Ai;

/// <summary>Grounds the app AI assistant on the real event: it reuses the SAME
/// public read services the app's own agenda / FAQ / exhibition screens call
/// (<see cref="IProgrammeSessionService"/>, <see cref="IPublicFaqService"/>,
/// <see cref="IPublicBoothService"/>), so the assistant can only ever cite active,
/// real data. Emits one bilingual line per fact via the shared
/// <see cref="AiGroundingText"/> primitive, which caps the text a safe margin below
/// the AI input-value limit and truncates on a whole-line boundary (the same
/// discipline as the CP assistant's page directory).</summary>
internal sealed class AssistanceContextBuilder(
    IProgrammeSessionService sessions,
    IPublicFaqService faq,
    IPublicBoothService booths) : IAssistanceContextBuilder
{
    public async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        var agenda = await sessions.ListAsync(day: null, categoryId: null, cancellationToken);
        var faqGroups = await faq.GetAsync(cancellationToken);
        var boothList = await booths.ListAsync(cancellationToken);

        var builder = new StringBuilder();

        AiGroundingText.AppendCappedSection(builder,
            "## Programme sessions (title EN / AR · start-end · hall)",
            agenda.Items.Select(session =>
                $"- {session.Title} / {session.TitleArabic} · "
                + $"{Stamp(session.Start)}-{session.End.ToString("HH:mm", CultureInfo.InvariantCulture)} · "
                + $"{session.HallName} / {session.HallNameArabic}"));

        AiGroundingText.AppendCappedSection(builder,
            "## FAQ (question → answer, EN / AR)",
            faqGroups.SelectMany(group => group.Entries).Select(entry =>
                $"- Q: {entry.Question} / {entry.QuestionArabic} "
                + $"— A: {entry.Answer} / {entry.AnswerArabic}"));

        AiGroundingText.AppendCappedSection(builder,
            "## Exhibition booths (code · name EN / AR · hall)",
            boothList.Select(booth =>
                $"- {booth.Code} · {booth.Name} / {booth.NameArabic}"
                + (string.IsNullOrWhiteSpace(booth.HallName)
                    ? string.Empty
                    : $" · {booth.HallName} / {booth.HallNameArabic}")));

        return builder.ToString().TrimEnd();
    }

    private static string Stamp(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
