// Tests: SIMF.Infrastructure.Ai.AssistanceContextBuilder (assistance grounding)
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Application.Faq.Abstractions;
using SIMF.Application.Programme.Abstractions;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Programme;
using SIMF.Infrastructure.Ai;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>The app AI assistant is grounded on the real event via
/// <see cref="AssistanceContextBuilder"/>: it must fold the live sessions, FAQ, and
/// booths into one bilingual context block, and stay under the shared AI input-value
/// cap while never half-emitting a fact.</summary>
public sealed class AssistanceContextBuilderTests
{
    [Fact]
    public async Task Build_includes_real_sessions_faq_and_booths_bilingual()
    {
        var builder = new AssistanceContextBuilder(
            new FakeSessions(new PublicSessionListItem(
                Guid.NewGuid(), "S1", "Opening Session", "جلسة الافتتاح",
                Guid.NewGuid(), "Main Hall", "القاعة الرئيسية",
                new DateTime(2027, 1, 20, 8, 0, 0),
                new DateTime(2027, 1, 20, 9, 0, 0),
                null, null, null)),
            new FakeFaq(new PublicFaqEntry(
                Guid.NewGuid(), "Where is parking?", "أين موقف السيارات؟",
                "Gate B.", "البوابة ب.")),
            new FakeBooths(new PublicBoothSummary
            {
                Code = "A-12", Name = "SAMI", NameArabic = "سامي",
                HallName = "Hall A", HallNameArabic = "القاعة أ",
            }));

        var context = await builder.BuildAsync();

        Assert.Contains("Opening Session", context);
        Assert.Contains("جلسة الافتتاح", context);
        Assert.Contains("2027-01-20 08:00-09:00", context);
        Assert.Contains("Main Hall", context);
        Assert.Contains("Where is parking?", context);
        Assert.Contains("أين موقف السيارات؟", context);
        Assert.Contains("A-12", context);
        Assert.Contains("SAMI", context);
    }

    [Fact]
    public async Task Build_stays_within_the_input_value_cap_and_never_half_emits_a_line()
    {
        // Far more FAQ entries than fit — the builder must cap under the shared
        // input-value limit and only ever drop whole lines.
        var manyEntries = Enumerable.Range(0, 400)
            .Select(i => new PublicFaqEntry(
                Guid.NewGuid(),
                $"Question number {i} with some length to it",
                $"سؤال رقم {i} بطول معقول",
                $"Answer number {i} with some length to it as well",
                $"جواب رقم {i} بطول معقول"))
            .ToArray();

        var builder = new AssistanceContextBuilder(
            new FakeSessions(), new FakeFaq(manyEntries), new FakeBooths());

        var context = await builder.BuildAsync();

        Assert.True(context.Length <= AiInputLimits.MaxInputValueLength,
            $"context was {context.Length} chars — must stay under the input-value cap.");
        // Every emitted FAQ line is a complete "- Q: ... — A: ..." (no half line).
        foreach (var line in context.Split('\n'))
        {
            Assert.False(line.StartsWith("- Q:", StringComparison.Ordinal)
                    && !line.Contains("— A:", StringComparison.Ordinal),
                $"a FAQ line was half-emitted: {line}");
        }
    }

    [Fact]
    public async Task Build_returns_empty_when_there_is_no_event_data()
    {
        var builder = new AssistanceContextBuilder(
            new FakeSessions(), new FakeFaq(), new FakeBooths());

        Assert.Equal(string.Empty, await builder.BuildAsync());
    }
}

file sealed class FakeSessions(params PublicSessionListItem[] items)
    : IProgrammeSessionService
{
    public Task<PublicSessions> ListAsync(
        DateOnly? day, Guid? categoryId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PublicSessions(items));

    // Unused by the context builder.
    public Task<PublicProgrammeDays> ListDaysAsync(CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<PublicSessionDetail?> GetAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();
    public Task<SessionRecordingRef?> GetPublishedRecordingAsync(
        Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<PublicRecordedQuestion>> ListRecordedQuestionsAsync(
        Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<PublicSessionSummary?> GetSessionSummaryAsync(
        Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<HostSessionSummary?> GetApprovedSummaryForHostAsync(
        Guid callerUserId, Guid sessionId, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

file sealed class FakeFaq(params PublicFaqEntry[] entries) : IPublicFaqService
{
    public Task<IReadOnlyList<PublicFaqGroup>> GetAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PublicFaqGroup>>(entries.Length == 0
            ? []
            : [new PublicFaqGroup(Guid.NewGuid(), "General", "عام", entries)]);
}

file sealed class FakeBooths(params PublicBoothSummary[] booths) : IPublicBoothService
{
    public Task<IReadOnlyList<PublicBoothSummary>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PublicBoothSummary>>(booths);

    public Task<PublicBoothDetail?> GetAsync(Guid id, CancellationToken ct = default) =>
        throw new NotSupportedException();
}
