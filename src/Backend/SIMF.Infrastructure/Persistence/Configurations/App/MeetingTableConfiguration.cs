using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-013 — D-248: MeetingTable EF config. Real FK to Hall
/// (Restrict — halls are soft-deleted, never hard-deleted under a table). A
/// filtered unique index keeps the table <c>Code</c> unique among active tables in
/// the same hall.</summary>
internal sealed class MeetingTableConfiguration : IEntityTypeConfiguration<MeetingTable>
{
    public void Configure(EntityTypeBuilder<MeetingTable> builder)
    {
        builder.ToTable("MeetingTables");
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
