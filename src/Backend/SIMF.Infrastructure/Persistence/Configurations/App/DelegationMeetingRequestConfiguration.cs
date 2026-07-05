using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-478 (#11, Group G phase 2) — DelegationMeetingRequest EF config. Real
/// FKs to Country for both the requesting and target countries (Restrict — a
/// country in use can't be hard-deleted); RequestedByUserId is a logical FK to
/// SimfUser on the Identity DB (no cross-DB relation, D-157).</summary>
internal sealed class DelegationMeetingRequestConfiguration
    : IEntityTypeConfiguration<DelegationMeetingRequest>
{
    public void Configure(EntityTypeBuilder<DelegationMeetingRequest> builder)
    {
        // D-611 (Wave B) — the proposed slot must end after it starts (both nullable).
        builder.ToTable("DelegationMeetingRequests", table => table.HasCheckConstraint(
            "CK_DelegationMeetingRequests_Slot",
            "[SlotStartUtc] IS NULL OR [SlotEndUtc] IS NULL OR [SlotEndUtc] > [SlotStartUtc]"));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasOne(r => r.RequestingCountry)
            .WithMany()
            .HasForeignKey(r => r.RequestingCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetCountry)
            .WithMany()
            .HasForeignKey(r => r.TargetCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TargetCountryId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
