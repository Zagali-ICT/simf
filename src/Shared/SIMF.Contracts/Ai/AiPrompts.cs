using SIMF.Common.Enums;

namespace SIMF.Contracts.Ai;

/// <summary>D-176 (gap doc G12) — admin grid row.</summary>
public sealed record AdminAiPromptSummary(
    Guid Id,
    string Key,
    AiFeature Feature,
    string DisplayName,
    string DisplayNameArabic,
    AiProvider Provider,
    string Model,
    double Temperature,
    int MaxOutputTokens,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-176 — full detail row (read).</summary>
public sealed record AdminAiPromptDetail(
    Guid Id,
    string Key,
    AiFeature Feature,
    string DisplayName,
    string DisplayNameArabic,
    string? Description,
    string? DescriptionArabic,
    AiProvider Provider,
    string Model,
    string SystemPrompt,
    string UserPromptTemplate,
    double Temperature,
    int MaxOutputTokens,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>D-176 — create admin request. Open for inheritance per
/// D-168 / D-174 / D-175 pattern.</summary>
public class CreateAiPromptRequest
{
    public string Key { get; set; } = string.Empty;
    public AiFeature Feature { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string DisplayNameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public AiProvider Provider { get; set; } = AiProvider.Echo;
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.2;
    public int MaxOutputTokens { get; set; } = 512;
}

/// <summary>D-176 — update request (same shape as create, no Key
/// because Key is immutable once written).</summary>
public class UpdateAiPromptRequest
{
    public AiFeature Feature { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string DisplayNameArabic { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPromptTemplate { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxOutputTokens { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>D-176 — admin dry-run a prompt with arbitrary inputs.</summary>
public class TestAiPromptRequest
{
    public Dictionary<string, string> Inputs { get; set; } = new();
}

/// <summary>D-176 — result of an AI call shown to the admin tester
/// + the feature endpoints.</summary>
public sealed record AiCallResult(
    Guid InvocationId,
    string PromptKey,
    AiFeature Feature,
    AiProvider Provider,
    string Model,
    string OutputText,
    int? TokensInput,
    int? TokensOutput,
    int LatencyMs);

/// <summary>D-176 — admin invocations log row.</summary>
public sealed record AdminAiInvocationRow(
    Guid Id,
    string PromptKey,
    AiFeature Feature,
    AiProvider Provider,
    string Model,
    string CallerKind,
    Guid? CallerUserId,
    int? TokensInput,
    int? TokensOutput,
    int LatencyMs,
    string? ErrorCode,
    DateTimeOffset CreatedAt);

// Feature request bodies ------------------------------------------------

public class FilterQuestionRequest
{
    public string Text { get; set; } = string.Empty;
}

public class AskFaqRequest
{
    public string Question { get; set; } = string.Empty;
}

public class AssistanceRequest
{
    public string Message { get; set; } = string.Empty;
}

public class TranslateRequest
{
    public string Text { get; set; } = string.Empty;
    public string SourceLang { get; set; } = "en";
    public string TargetLang { get; set; } = "ar";
}

public sealed class LiveTranslateChunkRequest
{
    public string Text { get; set; } = string.Empty;
    public string SourceLang { get; set; } = "en";
    public string TargetLang { get; set; } = "ar";
    public string? SessionId { get; set; }
}

public sealed class LiveSignChunkRequest
{
    public string Text { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}
