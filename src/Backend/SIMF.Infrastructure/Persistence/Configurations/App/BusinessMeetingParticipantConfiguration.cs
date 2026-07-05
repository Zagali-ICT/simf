using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>SIMF-FDS-013 — D-248: BusinessMeetingParticipant EF config. The
/// parent meeting FK is configured on <see cref="BusinessMeetingConfiguration"/>
/// (cascade). <c>ExhibitorId</c> is an optional real FK to Exhibitor (Restrict);
/// <c>VisitorUserId</c> is a bare logical Guid to the Identity DB — no navigation,
/// no FK (D-157 / D-246). Lookup indexes back the participant-overlap check.</summary>
internal sealed class BusinessMeetingParticipantConfiguration
    : IEntityTypeConfiguration<BusinessMeetingParticipant>
{
    public void Configure(EntityTypeBuilder<BusinessMeetingParticipant> builder)
    {
        // D-611 (Wave B) — exactly one party column is set, matching Kind (Company=0 / Visitor=1).
        builder.ToTable("BusinessMeetingParticipants", table => table.HasCheckConstraint(
            "CK_BusinessMeetingParticipants_PartyXor",
            "([Kind] = 0 AND [ExhibitorId] IS NOT NULL AND [VisitorUserId] IS NULL) OR ([Kind] = 1 AND [VisitorUserId] IS NOT NULL AND [ExhibitorId] IS NULL)"));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DisplayNameSnapshot).HasMaxLength(256).IsRequired();

        builder.HasOne(p => p.Exhibitor)
            .WithMany()
            .HasForeignKey(p => p.ExhibitorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.BusinessMeetingId);
        builder.HasIndex(p => p.ExhibitorId);
        builder.HasIndex(p => p.VisitorUserId);

        // D-611 (Wave B) — a company / a visitor appears at most once per meeting.
        builder.HasIndex(p => new { p.BusinessMeetingId, p.ExhibitorId })
            .IsUnique()
            .HasFilter("[ExhibitorId] IS NOT NULL");
        builder.HasIndex(p => new { p.BusinessMeetingId, p.VisitorUserId })
            .IsUnique()
            .HasFilter("[VisitorUserId] IS NOT NULL");
    }
}
