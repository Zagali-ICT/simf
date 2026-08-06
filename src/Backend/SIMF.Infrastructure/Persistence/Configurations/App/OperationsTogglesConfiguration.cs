using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Operations;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Singleton tables. Both rows are seeded
/// in EF model data so the gate exists from day-one.</summary>
internal sealed class RegistrationGateConfiguration
    : IEntityTypeConfiguration<RegistrationGate>
{
    public void Configure(EntityTypeBuilder<RegistrationGate> builder)
    {
        builder.ToTable("RegistrationGate");
        builder.HasKey(g => g.Id);
        builder.HasData(new RegistrationGate
        {
            Id = RegistrationGate.SingletonId,
            IsOpen = true,
            AutoClose = null,
            LastChangedAt = new DateTime(2026, 1, 1, 0, 0, 0),
            LastChangedByUserId = null,
        });
    }
}

internal sealed class ArchiveVisibilityConfiguration
    : IEntityTypeConfiguration<ArchiveVisibility>
{
    public void Configure(EntityTypeBuilder<ArchiveVisibility> builder)
    {
        builder.ToTable("ArchiveVisibility");
        builder.HasKey(a => a.Id);
        builder.HasData(new ArchiveVisibility
        {
            Id = ArchiveVisibility.SingletonId,
            IsVisible = true,
            LastChangedAt = new DateTime(2026, 1, 1, 0, 0, 0),
            LastChangedByUserId = null,
        });
    }
}
