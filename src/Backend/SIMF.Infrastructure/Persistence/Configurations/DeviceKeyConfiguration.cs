using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>D-172 (gap doc G10) — DeviceKey entity configuration on
/// the Identity DbContext. Real FK to SimfUser with cascade so a user
/// delete cleans up their device keys.</summary>
internal sealed class DeviceKeyConfiguration : IEntityTypeConfiguration<DeviceKey>
{
    public void Configure(EntityTypeBuilder<DeviceKey> builder)
    {
        builder.ToTable("DeviceKeys");
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
            .HasForeignKey(k => k.CreatedAt)
            .OnDelete(DeleteBehavior.Cascade);

        // The hot-path lookup is "list my non-revoked keys" — covered
        // by this composite.
        builder.HasIndex(k => new { k.CreatedAt, k.RevokedAt });
    }
}
