using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;
using SIMF.Domain.Profiles;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Composite key (GateId, ProfileTypeId).
/// After D-167 moved <c>ProfileType</c> onto <c>SimfAppDbContext</c>,
/// the <c>ProfileTypeId</c> FK is real (same-DB) with Restrict.</summary>
internal sealed class GateProfileTypeAllowConfiguration
    : IEntityTypeConfiguration<GateProfileTypeAllow>
{
    public void Configure(EntityTypeBuilder<GateProfileTypeAllow> builder)
    {
        builder.ToTable("GateProfileTypeAllow");
        builder.HasKey(allow => new { allow.GateId, allow.ProfileTypeId });
        builder.HasOne<UserProfileType>()
            .WithMany()
            .HasForeignKey(allow => allow.ProfileTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
