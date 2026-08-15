using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>One bookable table inside a Meeting or General hall, where a business meeting is scheduled for a time slot.</summary>
public sealed class MeetingTable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>Row-and-column placement; <see cref="ColumnNumber"/> is one-based within the row.</summary>
    public string? RowLabel { get; set; }

    public int? ColumnNumber { get; set; }

    public int Capacity { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
