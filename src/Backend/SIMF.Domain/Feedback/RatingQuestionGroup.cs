using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// An optional grouping of rating questions within a <see cref="RatingType"/>
/// (e.g. "Speaker", "Sound", "Light" under the Session type). Questions whose
/// <see cref="RatingQuestion.RatingQuestionGroupId"/> is null render as a flat
/// list — groups are purely a presentation grouping. Bilingual, orderable,
/// soft-deletable; mirrors <c>FaqGroup</c>.
/// </summary>
public sealed class RatingQuestionGroup : BaseAuditEntity
{
    /// <summary>Owning rating type (real DB FK — same DbContext).</summary>
    public Guid RatingTypeId { get; set; }
    public RatingType? Type { get; set; }

    /// <summary>English group name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic group name — paired with <see cref="Name"/>.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Sort key within the type's groups (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>The group's questions.</summary>
    public ICollection<RatingQuestion> Questions { get; set; } = new List<RatingQuestion>();
}
