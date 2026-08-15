using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>An admin-defined rating context (App, Session): its questions, groups and
/// comment toggle are data, not schema. <see cref="Scope"/> sets the submission key —
/// Global is once per user, PerSession once per user per target session.</summary>
public sealed class RatingType : BaseAuditEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public RatingScope Scope { get; set; }

    public bool HasOverallStars { get; set; } = true;

    public bool AllowComment { get; set; } = true;

    /// <summary>Null falls back to a generic localized "Comments (optional)" label.</summary>
    public string? CommentLabel { get; set; }

    public string? CommentLabelArabic { get; set; }

    /// <summary>Seeded system types (App / Session) cannot be deleted, and their
    /// <see cref="Code"/> — the slug the app and workers resolve by — is locked.</summary>
    public bool IsSystem { get; set; }

    public int DisplayOrder { get; set; }

    public ICollection<RatingQuestionGroup> Groups { get; set; } = new List<RatingQuestionGroup>();

    public ICollection<RatingQuestion> Questions { get; set; } = new List<RatingQuestion>();
}
