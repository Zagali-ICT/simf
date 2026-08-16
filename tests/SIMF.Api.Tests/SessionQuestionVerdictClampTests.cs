// Guards the write site of SessionQuestion.AiFilterVerdict.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.SessionQuestions.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Sessions;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using SIMF.Infrastructure.SessionQuestions;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// <c>SessionQuestion.AiFilterVerdict</c> is <c>nvarchar(256)</c> and was filled
/// verbatim from whatever <see cref="IQuestionAiFilter"/> returned. The shipped
/// <c>StubQuestionAiFilter</c> answers in short buckets, so the column never
/// overflowed in practice — but the interface promises nothing about length, and
/// swapping in a real provider (the documented DI/config swap) is exactly how a
/// long verdict arrives. An over-long verdict reached SaveChanges and SQL Server
/// rejected the INSERT outright, turning an ADVISORY tag into a failed submit:
/// the audience member loses their question because a filter that "never blocks"
/// was too wordy. The verdict is clamped at the write site instead.
///
/// <para>The service is constructed directly rather than driven over HTTP so the
/// filter can be faked without touching the shared API fixture.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Programme)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class SessionQuestionVerdictClampTests : IClassFixture<SimfApiFactory>
{
    /// <summary>Mirrors SessionQuestionConfiguration's HasMaxLength(256).</summary>
    private const int VerdictColumnLength = 256;

    private readonly SimfApiFactory _factory;

    public SessionQuestionVerdictClampTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task An_over_long_ai_verdict_is_clamped_and_the_question_still_lands()
    {
        var longVerdict = new string('v', 400);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var sessionId = await SeedFutureSessionAsync(db);

        var service = new SessionQuestionService(
            db,
            new _NoOpAuditLog(),
            TimeProvider.System,
            new _FixedVerdictFilter(longVerdict),
            NullLogger<SessionQuestionService>.Instance);

        // A PRE (future-session) question is the path that runs the AI filter.
        var submitted = await service.SubmitAsync(
            sessionId,
            Guid.NewGuid(),
            new SubmitSessionQuestionRequest { QuestionText = "Does a long verdict fit?" });

        var stored = await db.SessionQuestions
            .AsNoTracking()
            .SingleAsync(q => q.Id == submitted.Id);

        Assert.NotNull(stored.AiFilterVerdict);
        Assert.Equal(VerdictColumnLength, stored.AiFilterVerdict!.Length);
        Assert.Equal(longVerdict[..VerdictColumnLength], stored.AiFilterVerdict);
    }

    [Fact]
    public async Task A_short_verdict_is_stored_untouched()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var sessionId = await SeedFutureSessionAsync(db);

        var service = new SessionQuestionService(
            db,
            new _NoOpAuditLog(),
            TimeProvider.System,
            new _FixedVerdictFilter("ai-clean"),
            NullLogger<SessionQuestionService>.Instance);

        var submitted = await service.SubmitAsync(
            sessionId,
            Guid.NewGuid(),
            new SubmitSessionQuestionRequest { QuestionText = "Short verdict path" });

        var stored = await db.SessionQuestions
            .AsNoTracking()
            .SingleAsync(q => q.Id == submitted.Id);
        Assert.Equal("ai-clean", stored.AiFilterVerdict);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>A hall with no geofence plus a session that has not started, so the
    /// submit takes the PRE branch: no venue gate, and the AI filter runs.</summary>
    private static async Task<Guid> SeedFutureSessionAsync(SimfAppDbContext db)
    {
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Verdict Hall",
            NameArabic = "قاعة التحقق",
            Capacity = 25,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Halls.Add(hall);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Code = "SES-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Title = "Verdict Session",
            TitleArabic = "جلسة التحقق",
            HallId = hall.Id,
            Start = SimfClock.Now.AddDays(1),
            End = SimfClock.Now.AddDays(1).AddHours(1),
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private sealed class _FixedVerdictFilter(string verdict) : IQuestionAiFilter
    {
        public Task<QuestionAiVerdict> ScreenAsync(
            Guid sessionId,
            Guid userId,
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QuestionAiVerdict(verdict));
    }

    private sealed class _NoOpAuditLog : IAuditLog
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
