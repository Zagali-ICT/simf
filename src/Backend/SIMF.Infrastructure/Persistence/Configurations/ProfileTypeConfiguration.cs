using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for <see cref="ProfileType"/> (P7 — D-048).</summary>
internal sealed class ProfileTypeConfiguration : IEntityTypeConfiguration<ProfileType>
{
    public void Configure(EntityTypeBuilder<ProfileType> builder)
    {
        builder.ToTable("ProfileTypes");

        builder.HasKey(profileType => profileType.Id);

        builder.Property(profileType => profileType.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(profileType => profileType.NameArabic)
            .HasMaxLength(128)
            .IsRequired();

        // A hex string like "#FFD700" or a CSS variable name —
        // 32 chars is comfortable for both.
        builder.Property(profileType => profileType.PageColor)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(profileType => profileType.UserType)
            .HasConversion<string>()
            .HasMaxLength(16);

        // D-161 — MobileAppRole persisted as the stringly enum value
        // (None / Visitor / Staff / Moderator) so a DBA reading the
        // table can interpret the column without an out-of-band
        // mapping table. Default None keeps backfill safe.
        builder.Property(profileType => profileType.MobileAppRole)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(MobileAppRole.None)
            .IsRequired();

        // The picker filters by (UserType, IsActive) — keep one
        // composite index so the dropdown is one lookup.
        builder.HasIndex(profileType => new { profileType.UserType, profileType.IsActive });
    }
}
