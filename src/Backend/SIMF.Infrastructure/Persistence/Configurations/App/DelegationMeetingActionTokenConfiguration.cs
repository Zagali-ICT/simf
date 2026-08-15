using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.BusinessMeetings;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>DelegationMeetingActionToken EF config.
/// Real FK to DelegationMeetingRequest with cascade so a deleted request removes its
/// tokens. Only the keyed-HMAC hash is stored; redemption looks up by that hash, so
/// it is unique-indexed. Mirrors MeetingActionTokenConfiguration (the speaker token);
/// a new additive table that leaves the frozen speaker table untouched.</summary>
internal sealed class DelegationMeetingActionTokenConfiguration
    : IEntityTypeConfiguration<DelegationMeetingActionToken>
{
    public void Configure(EntityTypeBuilder<DelegationMeetingActionToken> builder)
    {
        builder.HasKey(t => t.Id);

        // HMAC-SHA256 lowercase-hex digest: always exactly 64 ASCII chars, so
        // char(64) rather than nvarchar — half the bytes and no length variance
        // on the column redemption looks the token up by.
        builder.Property(t => t.TokenHash)
            .HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();

        builder.HasOne(t => t.DelegationMeetingRequest)
            .WithMany()
            .HasForeignKey(t => t.DelegationMeetingRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Redemption is a lookup by hash; unique so no two tokens can share one.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.DelegationMeetingRequestId);
    }
}
