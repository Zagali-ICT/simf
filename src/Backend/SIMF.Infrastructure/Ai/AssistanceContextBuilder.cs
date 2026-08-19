// Tests: SIMF.Api.Tests/AssistanceContextBuilderTests.cs
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
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
/// discipline as the CP assistant's page directory).
///
/// <para>The three reads are the WHOLE programme, the WHOLE FAQ and the WHOLE
/// booth list — near-static content that only an admin edit changes — and the
/// builder runs on every single chat message. So the composed block is cached
/// for <see cref="Ttl"/> (the read-through shape <c>GateConfigCache</c> and the
/// organization-profile read already use): a few hundred attendees chatting
/// concurrently then cost three queries a minute instead of three per message,
/// on the same database that is serving check-in. The TTL is short because the
/// admin write paths for sessions / FAQ / booths do not invalidate it, so a
/// content edit must age out rather than be pushed out.</para></summary>
internal sealed class AssistanceContextBuilder(
    IProgrammeSessionService sessions,
    IPublicFaqService faq,
    IPublicBoothService booths,
    IMemoryCache cache) : IAssistanceContextBuilder
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);
    private const string CacheKey = "ai-assistance-grounding:v1";

    public async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<string>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var context = await ComposeAsync(cancellationToken);
        cache.Set(CacheKey, context, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Ttl,
        });
        return context;
    }

    private async Task<string> ComposeAsync(CancellationToken cancellationToken)
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
