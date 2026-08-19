using SIMF.Application.Auditing;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Cover for the batch write's DEFAULT body on <see cref="IAuditLog"/> —
/// the path taken by any implementation that has nothing to batch, which is every
/// test double in this suite. The real store overrides it to save once; the
/// default has to keep writing one entry at a time, in order, or a double that
/// records what it was told would start missing entries. In process, no host.</summary>
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class AuditLogBatchFallbackTests
{
    [Fact]
    public async Task The_default_batch_write_writes_every_entry_in_order()
    {
        var log = new SingleEntryAuditLog();

        await ((IAuditLog)log).WriteManyAsync([Entry("first"), Entry("second"), Entry("third")]);

        Assert.Equal(new List<string> { "first", "second", "third" }, log.Details);
    }

    [Fact]
    public async Task The_default_batch_write_of_an_empty_set_writes_nothing()
    {
        var log = new SingleEntryAuditLog();

        await ((IAuditLog)log).WriteManyAsync([]);

        Assert.Empty(log.Details);
    }

    private static AuditEntry Entry(string detail) =>
        new()
        {
            EventType = AuditEvents.SeatReservationReleased,
            Outcome = AuditOutcome.Success,
            Detail = detail,
        };

    /// <summary>An implementation that supplies ONLY the single-entry write. It is
    /// the shape the interface's default body exists for: adding a batch method
    /// must not force every double in the suite to hand-roll the same loop.</summary>
    private sealed class SingleEntryAuditLog : IAuditLog
    {
        public List<string> Details { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Details.Add(entry.Detail ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}
