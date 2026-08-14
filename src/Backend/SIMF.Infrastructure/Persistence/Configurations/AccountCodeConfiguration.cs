// Tests: SIMF.Api.Tests/AuthFlow.cs (the Code column holds a hash, recovered
//        by brute force) and SIMF.Api.Tests/RetentionPurgeServiceTests.cs.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.IdentityAccess;

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

        builder.HasOne<SimfUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
