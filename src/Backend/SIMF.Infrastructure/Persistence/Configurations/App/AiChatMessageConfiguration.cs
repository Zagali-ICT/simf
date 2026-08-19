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
        // Role is a two-value set, written only by AiChatHistoryService's
        // RoleUser / RoleAssistant consts and read back onto the AiChatTurn wire
        // contract. Bounded here so a third value cannot be inserted at all.
        builder.ToTable("AiChatMessages", table => table.HasCheckConstraint(
            "CK_AiChatMessages_Role", "[Role] IN ('user', 'assistant')"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
        // The column TYPE is set in SimfAppDbContext.OnModelCreating, beside the
        // value converter that encrypts it: the ciphertext envelope does not fit
        // the 4000-character ceiling nvarchar allows, so this is nvarchar(max).
        // Declaring a length here as well would fight that registration.
        builder.Property(x => x.Content).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
