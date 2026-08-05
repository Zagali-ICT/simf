using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>
/// One named, versioned, editable AI prompt. An admin can change its content,
/// provider, model and limits from the Control Panel at runtime, without a
/// redeploy.
/// </summary>
public sealed class AiPrompt
{
    public Guid Id { get; set; }

    /// <summary>The stable slug callers reference, such as
    /// <c>question-filter</c>. Kebab-case and unique.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Which feature this prompt is registered against.</summary>
    public AiFeature Feature { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string DisplayNameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public AiProvider Provider { get; set; }

    /// <summary>A provider-specific model identifier, such as
    /// <c>gpt-4o-mini</c>.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>A template: <c>{placeholder}</c> spans are substituted from
    /// caller inputs.</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>The user turn, following the same placeholder rules as
    /// <see cref="SystemPrompt"/>.</summary>
    public string UserPromptTemplate { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public int MaxOutputTokens { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Incremented on every successful save, so the Control Panel can
    /// show a version label and a test can assert a new one was written.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
