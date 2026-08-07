using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Programme;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>Session favourites (المفضلة) configuration. Unique per
/// (UserId, SessionId) so a toggle is idempotent; cascade-deletes with the
/// session (admins soft-delete sessions, so cascade only guards a hard delete).
/// No FK on UserId — it is a bare Guid to the Identity DB.</summary>
internal sealed class SessionFavouriteConfiguration
    : IEntityTypeConfiguration<SessionFavourite>
{
    public void Configure(EntityTypeBuilder<SessionFavourite> builder)
    {
        builder.ToTable("SessionFavourites");
        builder.HasKey(f => f.Id);

        builder.HasIndex(f => new { f.UserId, f.SessionId }).IsUnique();

        builder.HasOne(f => f.Session)
            .WithMany()
            .HasForeignKey(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
