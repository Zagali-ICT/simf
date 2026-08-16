// D-611 (Wave B) — smoke tests proving the new DB-level backstops actually fire:
// a filtered-unique index rejects a duplicate active row, and a CHECK constraint
// rejects a reversed time window. The full suite proves the complementary side
// (no valid row is wrongly rejected) by applying these migrations to every test DB.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class WaveBConstraintTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public WaveBConstraintTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Duplicate_active_programme_day_for_the_same_date_is_rejected()
    {
        var date = new DateOnly(2027, 3, 1);

        await AddAndSaveAsync(new ProgrammeDay
        {
            Id = Guid.NewGuid(),
            Date = date,
            Title = "Day A",
            TitleArabic = "يوم أ",
            IsActive = true,
            DisplayOrder = 1,
        });

        // A second ACTIVE day on the same date violates the filtered unique
        // IX_ProgrammeDays_Date (WHERE [IsActive] = 1).
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAndSaveAsync(new ProgrammeDay
        {
            Id = Guid.NewGuid(),
            Date = date,
            Title = "Day B",
            TitleArabic = "يوم ب",
            IsActive = true,
            DisplayOrder = 2,
        }));
    }

    [Fact]
    public async Task Speaker_availability_window_with_a_reversed_time_range_is_rejected()
    {
        var speakerId = Guid.NewGuid();
        await AddAndSaveAsync(new Speaker
        {
            Id = speakerId,
            Code = speakerId.ToString("N")[..16],
            Name = "Window Owner",
            NameArabic = "المتحدث",
        });

        var start = SimfClock.Now;

        // End <= Start violates CK_SpeakerAvailabilityWindows_TimeWindow.
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAndSaveAsync(new SpeakerAvailabilityWindow
        {
            Id = Guid.NewGuid(),
            SpeakerId = speakerId,
            Start = start,
            End = start, // zero-length → rejected ([End] > [Start])
            IsActive = true,
        }));
    }

    private async Task AddAndSaveAsync<TEntity>(TEntity entity) where TEntity : class
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync();
    }
}
