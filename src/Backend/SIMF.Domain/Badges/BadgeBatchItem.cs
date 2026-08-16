using SIMF.Domain.Common;

namespace SIMF.Domain.Badges;

/// <summary>
/// One line of a <see cref="BadgeBatch"/>: how many badges the run minted for a
/// single profile type. An order that minted three VIP and two Normal badges has
/// two of these rows.
///
/// <para>These rows replaced a rendered <c>CountsSummary</c> string on the batch
/// ("VIP × 3 + Normal × 2"). That column crushed several facts into one value, so
/// no question could be asked of it — "how many VIP badges did we mint?" needed
/// string parsing — it was built invariant-culture, so an Arabic admin read an
/// English label, and it FROZE the profile-type name at mint time, which made
/// every historical label wrong the moment a tier was renamed. The counts live
/// here structured, and the readable breakdown is composed at READ time against
/// the live profile-type row, so a rename now fixes history instead of breaking
/// it.</para>
///
/// <para>A profile type may legitimately appear on two lines of one request, so
/// there is deliberately no unique key on (batch, profile type);
/// <see cref="DisplayOrder"/> keeps the lines in the order the admin entered
/// them. A top-up folds into the first line of a type the order already holds,
/// which is what the old string merge did by name.</para>
/// </summary>
public sealed class BadgeBatchItem : BaseEntity
{
    /// <summary>The order this line belongs to.</summary>
    public Guid BadgeBatchId { get; set; }

    public BadgeBatch? BadgeBatch { get; set; }

    /// <summary>The profile type (audience tier) the badges were minted for.
    /// A real intra-App foreign key to <c>ProfileTypes</c> — the whole point of
    /// the change is that the tier is referenced, not copied as text.</summary>
    public Guid ProfileTypeId { get; set; }

    /// <summary>How many badges of this profile type the order holds. Always
    /// positive: the generate path drops zero-count lines before planning.</summary>
    public int Count { get; set; }

    /// <summary>Zero-based position of this line within the order, so the composed
    /// breakdown reads back in the order the admin typed it — which is what the
    /// stored string preserved by construction.</summary>
    public int DisplayOrder { get; set; }
}
