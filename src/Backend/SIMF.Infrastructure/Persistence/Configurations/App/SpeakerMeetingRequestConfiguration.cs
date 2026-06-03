using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.MeetingRequests;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-269 (Mockup page 20) — SpeakerMeetingRequest EF config. Real FK
/// to Speaker with cascade so a deleted speaker removes its pending requests;
/// RequestedByUserId is a logical FK to SimfUser on the Identity DB (no cross-DB
/// relation, D-157). Mirrors <see cref="MeetingRequestConfiguration"/>.</summary>
internal sealed class SpeakerMeetingRequestConfiguration
    : IEntityTypeConfiguration<SpeakerMeetingRequest>
{
    public void Configure(EntityTypeBuilder<SpeakerMeetingRequest> builder)
    {
        builder.ToTable("SpeakerMeetingRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequesterName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasOne(r => r.Speaker)
            .WithMany()
            .HasForeignKey(r => r.SpeakerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.SpeakerId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
