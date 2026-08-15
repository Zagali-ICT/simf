using SIMF.Common.Enums;

namespace SIMF.Domain.Ai;

/// <summary>A named, versioned AI prompt, editable from the Control Panel at
/// runtime without a redeploy.</summary>
public sealed class AiPrompt
{
    public Guid Id { get; set; }

    /// <summary>The kebab-case slug callers reference, such as <c>question-filter</c>.</summary>
    public string Key { get; set; } = string.Empty;

    public AiFeature Feature { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string DisplayNameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    public AiProvider Provider { get; set; }

    public string Model { get; set; } = string.Empty;

    /// <summary>A template: <c>{placeholder}</c> spans are substituted from caller inputs.</summary>
    public string SystemPrompt { get; set; } = string.Empty;

    public string UserPromptTemplate { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public int MaxOutputTokens { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Incremented by the service on every successful save.</summary>
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
