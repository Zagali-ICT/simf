using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Ai;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>EF config for the per-user AI-assistant chat history (Page 036).
/// Indexed by (UserId, CreatedAt) for the two reads: the visitor's whole
/// transcript oldest-first, and the recent-turns memory window.</summary>
internal sealed class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("AiChatMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Content).HasMaxLength(4000).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
