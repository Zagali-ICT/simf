using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class SimfUserConfiguration : IEntityTypeConfiguration<SimfUser>
{
    public void Configure(EntityTypeBuilder<SimfUser> builder)
    {
        builder.Property(user => user.DisplayName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(user => user.AccountState)
            .HasConversion<string>()
            .HasMaxLength(32);

        // P7 — UserType + ProfileType (decision D-048).
        // UserType is stored as a string for readability in SQL diagnostics;
        // ProfileTypeId is a plain FK column (no nav property — the join is
        // explicit on the service layer).
        builder.Property(user => user.UserType)
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.HasIndex(user => user.UserType);

        builder.HasOne<ProfileType>()
            .WithMany()
            .HasForeignKey(user => user.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // D-046: short opaque event-entry id, minted on Approved.
        builder.Property(user => user.QrId)
            .HasMaxLength(16);
        builder.HasIndex(user => user.QrId)
            .IsUnique()
            .HasFilter("[QrId] IS NOT NULL");

        builder.Property(user => user.AvatarRelativePath)
            .HasMaxLength(256);
    }
}
