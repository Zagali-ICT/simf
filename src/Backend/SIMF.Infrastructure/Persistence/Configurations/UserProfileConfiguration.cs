using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the user-profile row (decisions D-046, P8 —
/// D-048; renamed from <c>VisitorProfileConfiguration</c>). Length caps
/// line up with the FluentValidation rules in
/// <c>UpsertUserProfileRequestValidator</c>. The <c>ProfileTypeId</c> FK
/// references the <c>ProfileTypes</c> lookup with
/// <c>OnDelete(Restrict)</c> so a profile-type cannot be deleted while
/// any user is assigned to it.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        // One profile per user — the column carries the unique constraint
        // so a second upsert by the same user updates instead of creating
        // a sibling row.
        builder.HasIndex(profile => profile.UserId).IsUnique();

        builder.Property(profile => profile.ArabicName).HasMaxLength(256).IsRequired();
        builder.Property(profile => profile.EnglishName).HasMaxLength(256).IsRequired();
        builder.Property(profile => profile.NationalityCode).HasMaxLength(2).IsRequired();
        builder.Property(profile => profile.PlaceOfBirth).HasMaxLength(128);
        builder.Property(profile => profile.NationalId).HasMaxLength(20);
        builder.Property(profile => profile.IqamaNumber).HasMaxLength(20);
        builder.Property(profile => profile.PassportNumber).HasMaxLength(32);
        builder.Property(profile => profile.SaudiMobile).HasMaxLength(20);
        builder.Property(profile => profile.InternationalMobile).HasMaxLength(24);
        builder.Property(profile => profile.IdImageRelativePath).HasMaxLength(256);

        // P8 — ProfileTypeId moved off SimfUser; lives here now.
        builder.HasIndex(profile => profile.ProfileTypeId);
        builder.HasOne(profile => profile.ProfileType)
            .WithMany()
            .HasForeignKey(profile => profile.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
