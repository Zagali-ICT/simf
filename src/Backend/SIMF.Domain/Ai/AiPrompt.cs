using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>D-176 (gap doc G12) — one named, versioned, editable AI
/// prompt. <see cref="Key"/> is the stable slug callers reference
/// (e.g. <c>question-filter</c>, <c>faq-answer</c>); the active
/// content (<see cref="SystemPrompt"/>, <see cref="UserPromptTemplate"/>,
/// <see cref="Provider"/>, <see cref="Model"/>, <see cref="Temperature"/>,
/// <see cref="MaxOutputTokens"/>) can be edited by an admin from the
/// CP at runtime — no redeploy needed.</summary>
public sealed class AiPrompt
{
    public Guid Id { get; set; }

    /// <summary>Stable slug, kebab-case, 2–64 chars. Unique.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Which feature this prompt is registered against.</summary>
    public AiFeature Feature { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string DisplayNameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public AiProvider Provider { get; set; }

    /// <summary>Provider-specific model identifier (e.g.
    /// <c>gpt-4o-mini</c>, <c>claude-opus-4-7</c>).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>System / role prompt. Template — supports
    /// <c>{placeholder}</c> substitutions from caller inputs.</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>User-turn template. Same <c>{placeholder}</c> rules
    /// as <see cref="SystemPrompt"/>.</summary>
    public string UserPromptTemplate { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public int MaxOutputTokens { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Bumped by 1 on every successful save. Lets the CP
    /// show a version label and helps the test rig assert that a
    /// new version was written.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
