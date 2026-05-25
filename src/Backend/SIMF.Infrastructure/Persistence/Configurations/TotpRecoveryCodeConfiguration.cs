using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class TotpRecoveryCodeConfiguration : IEntityTypeConfiguration<TotpRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TotpRecoveryCode> builder)
    {
        builder.HasKey(code => code.Id);

        // SHA-256 hash, base64 (44 chars). Fixed length so the index is tight.
        builder.Property(code => code.CodeHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(code => code.UserId);
        builder.HasIndex(code => code.CodeHash);

        // R5a — D-090: FK targets the IdentitySimfUser persistence shim
        // (see AccountCodeConfiguration for the full rationale).
        builder.HasOne<IdentitySimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
