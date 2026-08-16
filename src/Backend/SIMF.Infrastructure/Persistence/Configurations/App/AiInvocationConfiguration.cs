using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMF.Domain.Ai;

namespace SIMF.Infrastructure.Persistence.Configurations.App;

/// <summary>AiInvocation EF config. Append-only
/// telemetry. Indexes support the admin grid filters (by feature, by
/// caller, by time) + an error-only filter.</summary>
internal sealed class AiInvocationConfiguration : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        // CallerKind is a five-value set. Every writer supplies a literal:
        // AiCaller.FromUser yields Anonymous / Admin / Moderator / Staff /
        // Visitor, AiQuestionFilter writes Visitor, and the CP assistant plus
        // the admin prompt-run and session-summary services write Admin.
        // Bounded here so a typo at any of those sites fails the insert rather
        // than landing an unfilterable row in the telemetry log.
        builder.ToTable("AiInvocations", table => table.HasCheckConstraint(
            "CK_AiInvocations_CallerKind",
            "[CallerKind] IN ('Anonymous', 'Visitor', 'Staff', 'Admin', 'Moderator')"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PromptKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(64);
        builder.Property(x => x.CallerKind).HasMaxLength(16).IsRequired();
        // InputJson + OutputText are nvarchar(max) — variable size.

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.Feature, x.CreatedAt });
        builder.HasIndex(x => new { x.CallerUserId, x.CreatedAt });
        builder.HasIndex(x => new { x.ErrorCode, x.CreatedAt })
            .HasFilter("[ErrorCode] IS NOT NULL");
    }
}
