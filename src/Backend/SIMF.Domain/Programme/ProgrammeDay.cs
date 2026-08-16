using SIMF.Domain.Common;
using SIMF.Domain.Files;

namespace SIMF.Domain.Programme;

/// <summary>
/// One day of the forum programme. Deliberately no foreign key from
/// <see cref="Session"/>: sessions are matched to a day by date, so either side
/// renders alone when the other is missing.
/// </summary>
public class ProgrammeDay : BaseAuditEntity
{
    public DateOnly Date { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;

    /// <summary>Ascending, tie-broken by <see cref="Date"/>.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Stamped when the day's rating batch is dispatched, so a restart
    /// cannot resend it.</summary>
    public DateTime? RatingPromptSent { get; set; }

    /// <summary>The day's banner image, in the one file store.
    ///
    /// <para>Until this column existed the link ran one way only: the
    /// <c>StoredFile</c> carried <c>OwnerEntityType</c>/<c>OwnerEntityId</c> back to
    /// this row and nothing here pointed at the file. That is a polymorphic pair no
    /// foreign key can constrain, so nothing stopped a file naming a row that had
    /// been deleted, and "which file is current" was decided in code rather than by
    /// the schema. <c>OwnerPointerSync</c> keeps this column in step for
    /// <c>FileService.ProgrammeDayImage</c>.</para></summary>
    public Guid? ImageFileId { get; set; }
    public StoredFile? ImageFile { get; set; }
}
