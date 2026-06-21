using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        // The Identity password hash (PBKDF2, base64) — generously sized.
        builder.Property(entry => entry.PasswordHash).HasMaxLength(512).IsRequired();

        builder.HasIndex(entry => new { entry.UserId, entry.CreatedAtUtc });

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
