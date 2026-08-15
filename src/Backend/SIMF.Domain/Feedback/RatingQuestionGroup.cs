using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>A presentation-only grouping of a <see cref="RatingType"/>'s questions
/// (Speaker, Sound, Light); ungrouped questions render as a flat list.</summary>
public sealed class RatingQuestionGroup : BaseAuditEntity
{
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public ICollection<RatingQuestion> Questions { get; set; } = new List<RatingQuestion>();
}
