using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>MeetingTable EF config. Real FK to Hall
/// (Restrict — halls are soft-deleted, never hard-deleted under a table). A
/// filtered unique index keeps the table <c>Code</c> unique among active tables in
/// the same hall.</summary>
internal sealed class MeetingTableConfiguration : IEntityTypeConfiguration<MeetingTable>
{
    public void Configure(EntityTypeBuilder<MeetingTable> builder)
    {
        // A table seats 2..100 — the range ValidateCapacity enforces on create,
        // update and bulk-generate alike, so this is a true backstop.
        // NOTE: ColumnNumber is documented 1-based but has NO check constraint on
        // purpose. Bulk-generate derives it from an admin-typed CSV via
        // SplitRowColumn, which happily parses "A0" to column 0, so a CK here
        // would turn bad input into a 500 instead of a validation error. It needs
        // the service-side guard first.
        builder.ToTable("MeetingTables", table => table.HasCheckConstraint(
            "CK_MeetingTables_Capacity",
            "[Capacity] >= 2 AND [Capacity] <= 100"));
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).HasMaxLength(16).IsRequired();
        builder.Property(t => t.RowLabel).HasMaxLength(8);

        builder.HasOne(t => t.Hall)
            .WithMany()
            .HasForeignKey(t => t.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        // Code unique among active tables in a hall (released/soft-deleted free it).
        builder.HasIndex(t => new { t.HallId, t.Code })
            .HasFilter("[IsActive] = 1")
            .IsUnique();

        builder.HasIndex(t => new { t.HallId, t.IsActive });
    }
}
