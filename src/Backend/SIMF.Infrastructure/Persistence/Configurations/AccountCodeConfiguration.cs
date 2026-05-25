using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Identity;

namespace SIMF.Infrastructure.Persistence.Configurations;

internal sealed class AccountCodeConfiguration : IEntityTypeConfiguration<AccountCode>
{
    public void Configure(EntityTypeBuilder<AccountCode> builder)
    {
        builder.HasKey(code => code.Id);

        builder.Property(code => code.Code).HasMaxLength(16).IsRequired();

        builder.Property(code => code.Purpose)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(code => new { code.UserId, code.Purpose });

        // R5a — D-090: FK targets the IdentitySimfUser persistence shim
        // (which now owns the AspNetUsers table) rather than the Domain
        // SimfUser. Without this, EF discovers SimfUser as a separate
        // entity and demands a duplicate "SimfUser" table.
        builder.HasOne<IdentitySimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
