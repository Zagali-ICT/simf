using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>MeetingActionToken EF config.
/// Real FK to SpeakerMeetingRequest with cascade so a deleted request removes its
/// tokens. Only the keyed-HMAC hash is stored; redemption looks up by that hash,
/// so it is unique-indexed.</summary>
internal sealed class MeetingActionTokenConfiguration
    : IEntityTypeConfiguration<MeetingActionToken>
{
    public void Configure(EntityTypeBuilder<MeetingActionToken> builder)
    {
        builder.HasKey(t => t.Id);

        // HMAC-SHA256 lowercase-hex digest: always exactly 64 ASCII chars, so
        // char(64) rather than nvarchar — half the bytes and no length variance
        // on the column redemption looks the token up by.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();

        builder.HasOne(t => t.SpeakerMeetingRequest)
            .WithMany()
            .HasForeignKey(t => t.SpeakerMeetingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Redemption is a lookup by hash; unique so no two tokens can share one.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.SpeakerMeetingRequestId);
    }
}
