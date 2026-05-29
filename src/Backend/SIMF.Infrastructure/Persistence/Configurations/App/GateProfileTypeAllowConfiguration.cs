using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.AccessControl;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>D-148 — composite key (GateId, ProfileTypeId). ProfileTypeId is a
/// logical FK only — referential integrity enforced by the service layer
/// (DAT-001 §5.3.1).</summary>
internal sealed class GateProfileTypeAllowConfiguration
    : IEntityTypeConfiguration<GateProfileTypeAllow>
{
    public void Configure(EntityTypeBuilder<GateProfileTypeAllow> builder)
    {
        builder.ToTable("GateProfileTypeAllow");
        builder.HasKey(allow => new { allow.GateId, allow.ProfileTypeId });
    }
}
