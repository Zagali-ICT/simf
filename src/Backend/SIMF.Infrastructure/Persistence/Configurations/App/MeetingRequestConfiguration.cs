using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-174 (gap doc G11) — MeetingRequest EF config. Real FK
/// to Session with cascade so a deleted session removes its
/// pending requests; RequestedByUserId is a logical FK to SimfUser
/// on the Identity DB.</summary>
internal sealed class MeetingRequestConfiguration : IEntityTypeConfiguration<MeetingRequest>
{
    public void Configure(EntityTypeBuilder<MeetingRequest> builder)
    {
        builder.ToTable("MeetingRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RequesterName).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.ResponseNote).HasMaxLength(2000);

        builder.HasOne(r => r.Session)
            .WithMany()
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.SessionId, r.Status, r.CreatedAt });
        builder.HasIndex(r => r.RequestedByUserId);
    }
}
