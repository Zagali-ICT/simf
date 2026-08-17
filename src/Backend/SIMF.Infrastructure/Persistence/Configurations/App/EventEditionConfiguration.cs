using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Editions;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>The open-edition singleton, seeded in EF model data so a year is
/// open from day one. Without a seeded row the gate's edition check would refuse
/// every badge on a fresh database, which is the opposite of the intent.</summary>
internal sealed class EventEditionConfiguration : IEntityTypeConfiguration<EventEdition>
{
    public void Configure(EntityTypeBuilder<EventEdition> builder)
    {
        // The year has to FIT: it is encoded into the badge as two bytes and read
        // by a scanner with no other way to ask, so a typo of "202" or "20265"
        // must never reach the column. EventEditionService refuses the same range
        // on write; the literals here and there must stay in step.
        builder.ToTable("EventEdition", table => table.HasCheckConstraint(
            "CK_EventEdition_Year",
            "[Year] BETWEEN 2000 AND 2999"));
        builder.HasKey(edition => edition.Id);

        // A fixed year and date, not "now": HasData is compared into the
        // migration, and a moving value would report a pending model change on
        // every check. An admin opens the real year on first use.
        builder.HasData(new EventEdition
        {
            Id = EventEdition.SingletonId,
            Year = 2026,
            OpenedAt = new DateTime(2026, 1, 1, 0, 0, 0),
            LastClosedAt = null,
            LastReissueCount = 0,
        });
    }
}
