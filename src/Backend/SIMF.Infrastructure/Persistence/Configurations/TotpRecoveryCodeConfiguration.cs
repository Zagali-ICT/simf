using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class TotpRecoveryCodeConfiguration : IEntityTypeConfiguration<TotpRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TotpRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);

        // SHA-256 hash, base64 (44 chars). Fixed length so the index is tight.
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();

        // D-610 (Wave B) — one row per (user, code-hash): the composite unique is
        // the DB backstop for "consume a recovery code once" and supersedes the
        // standalone HasIndex(UserId) (its left-prefix). The standalone
        // HasIndex(CodeHash) stays for a by-hash redemption lookup.
        builder.HasIndex(code => new { code.UserId, code.CodeHash }).IsUnique();
        builder.HasIndex(code => code.CodeHash);

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
