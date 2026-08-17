using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Networking;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Connection (networking) configuration. Surrogate Id
/// key; both user references are logical FKs to SimfUser on the Identity DB
/// (NO SQL constraint — it would be cross-DB). Indexes cover the two
/// "my connections" read directions, and the canonical pair carries the
/// one-active-connection-per-pair rule.</summary>
internal sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        // A visitor cannot connect with themselves. NetworkingService.RequestAsync
        // rejects it with a bilingual 400, and this is the backstop behind that
        // check so no other writer can put the row in.
        builder.ToTable("Connections", table => table.HasCheckConstraint(
            "CK_Connections_NotSelf", "[RequesterUserId] <> [TargetUserId]"));
        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.State);

        builder.HasIndex(connection => connection.RequesterUserId);
        builder.HasIndex(connection => connection.TargetUserId);

        // At most one ACTIVE connection per unordered pair. The service's
        // check-then-insert stays as the friendly 409 path; this is the backstop two
        // concurrent requests cannot race past, because sorting the ids collapses both
        // directions of the same relationship onto one key. Filtered on IsActive so a
        // removed or declined connection can be requested again, and on the sorted pair
        // being present so rows written before RequestAsync began filling it are ignored
        // rather than colliding on NULL.
        builder.HasIndex(connection => new
        {
            connection.PairLowUserId,
            connection.PairHighUserId,
        })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [PairLowUserId] IS NOT NULL");
    }
}
