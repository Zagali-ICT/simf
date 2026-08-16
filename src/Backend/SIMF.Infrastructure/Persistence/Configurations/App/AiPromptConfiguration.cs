using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Ai;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>AiPrompt EF config. Unique on Key.
/// Long-text columns use <c>nvarchar(max)</c> so prompt templates can
/// grow without a schema migration.</summary>
internal sealed class AiPromptConfiguration : IEntityTypeConfiguration<AiPrompt>
{
    public void Configure(EntityTypeBuilder<AiPrompt> builder)
    {
        // The two model-call ranges AdminAiPromptService rejects on write
        // (ClampTemperature 0-2, ClampMaxTokens 1-8000) existed only in that service,
        // so any other writer could store a value the provider would reject at call
        // time. The literals here and there must stay in step.
        builder.ToTable("AiPrompts", table =>
        {
            table.HasCheckConstraint(
                "CK_AiPrompts_Temperature",
                "[Temperature] >= 0 AND [Temperature] <= 2");
            table.HasCheckConstraint(
                "CK_AiPrompts_MaxOutputTokens",
                "[MaxOutputTokens] >= 1 AND [MaxOutputTokens] <= 8000");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayNameArabic).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512);
        builder.Property(x => x.DescriptionArabic).HasMaxLength(512);
        builder.Property(x => x.Model).HasMaxLength(64).IsRequired();
        // No HasMaxLength on SystemPrompt / UserPromptTemplate so EF
        // maps them to nvarchar(max) — they can run to thousands of chars.
        builder.Property(x => x.SystemPrompt).IsRequired();
        builder.Property(x => x.UserPromptTemplate).IsRequired();

        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => new { x.Feature, x.IsActive });
    }
}
