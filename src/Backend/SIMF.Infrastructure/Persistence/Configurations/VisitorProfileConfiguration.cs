using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for the visitor profile row (decision D-046, myComment
/// #18). Length caps line up with the FluentValidation rules in
/// <c>UpsertVisitorProfileRequestValidator</c>.
/// </summary>
internal sealed class VisitorProfileConfiguration : IEntityTypeConfiguration<VisitorProfile>
{
    public void Configure(EntityTypeBuilder<VisitorProfile> builder)
    {
        builder.HasKey(profile => profile.Id);

        // One profile per user — the column carries the unique constraint
        // so a second upsert by the same user updates instead of creating
        // a sibling row.
        builder.HasIndex(profile => profile.UserId).IsUnique();

        builder.Property(profile => profile.VisitorType).HasMaxLength(32).IsRequired();
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
    }
}
