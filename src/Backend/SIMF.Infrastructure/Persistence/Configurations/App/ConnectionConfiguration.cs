using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Networking;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>B6 — D-224: Connection (networking) configuration. Surrogate Id
/// key; both user references are logical FKs to SimfUser on the Identity DB
/// (NO SQL constraint — cross-DB, D-157). Duplicate-pair rejection is enforced
/// in the service (not a DB unique index) so a removed connection can be
/// re-requested. Indexes cover the two "my connections" read directions.</summary>
internal sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("Connections");
        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.State);

        builder.HasIndex(connection => connection.RequesterUserId);
        builder.HasIndex(connection => connection.TargetUserId);
    }
}
