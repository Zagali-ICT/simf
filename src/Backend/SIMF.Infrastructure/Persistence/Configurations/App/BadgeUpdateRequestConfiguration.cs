using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Requests;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-500 (Wave 5, الطلبات) — BadgeUpdateRequest EF config.
/// RequestedByUserId is a logical FK to SimfUser on the Identity DB (no cross-DB
/// relation, D-157). The requested title length matches UserProfile.JobTitle
/// (128). Mirrors <see cref="SpeakerMeetingRequestConfiguration"/>.</summary>
internal sealed class BadgeUpdateRequestConfiguration
    : IEntityTypeConfiguration<BadgeUpdateRequest>
{
    public void Configure(EntityTypeBuilder<BadgeUpdateRequest> builder)
    {
        builder.ToTable("BadgeUpdateRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequestedJobTitle).HasMaxLength(128).IsRequired();
        builder.Property(r => r.CurrentJobTitle).HasMaxLength(128);
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
