using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>DeviceKey entity configuration on
/// the Identity DbContext. Real FK to SimfUser with cascade so a user
/// delete cleans up their device keys.</summary>
internal sealed class DeviceKeyConfiguration : IEntityTypeConfiguration<DeviceKey>
{
    public void Configure(EntityTypeBuilder<DeviceKey> builder)
    {
        // The in-flight challenge is a pair: the nonce and its deadline are
        // written together when one is issued and cleared together when it is
        // consumed, revoked or superseded. Without the pin, a half-written row
        // would carry a nonce with no expiry, which the verify path only refuses
        // because it separately re-checks both for null. Anchored on IS NULL /
        // IS NOT NULL: a bare comparison against a nullable column evaluates to
        // UNKNOWN, and SQL Server passes UNKNOWN on a CHECK.
        builder.ToTable("DeviceKeys", table => table.HasCheckConstraint(
            "CK_DeviceKeys_ChallengePin",
            "([CurrentChallenge] IS NULL AND [ChallengeExpiresAt] IS NULL) OR "
            + "([CurrentChallenge] IS NOT NULL AND [ChallengeExpiresAt] IS NOT NULL)"));
        builder.HasKey(k => k.Id);

        // ES256 SubjectPublicKeyInfo DER fits in ~91 base64 chars;
        // 256 leaves headroom for a future Ed25519 / ML-DSA addition
        // without a schema migration.
        builder.Property(k => k.PublicKey).HasMaxLength(256).IsRequired();
        builder.Property(k => k.Algorithm).HasMaxLength(16).IsRequired();
        builder.Property(k => k.Label).HasMaxLength(64).IsRequired();
        builder.Property(k => k.CurrentChallenge).HasMaxLength(128);

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The hot-path lookup is "list my non-revoked keys" — covered
        // by this composite.
        builder.HasIndex(k => new { k.UserId, k.RevokedAt });

        // At most one ACTIVE (non-revoked) key per (user,
        // public-key): a filtered unique so re-enrolling a rotated key still
        // works after the old one is revoked (RevokedAt set → excluded).
        builder.HasIndex(k => new { k.UserId, k.PublicKey })
            .IsUnique()
            .HasFilter("[RevokedAt] IS NULL");
    }
}
