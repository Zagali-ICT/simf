using SIMF.Domain.Common;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>A window of time during which a delegation, identified by its country, is available to meet, divided into fixed-length bookable slots.</summary>
public sealed class DelegationAvailabilityWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int CountryId { get; set; }
    public Country? Country { get; set; }

    /// <summary>Saudi local time.</summary>
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public int SlotMinutes { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>A bare Guid: the admin lives in the Identity database.</summary>
    public Guid? CreatedByUserId { get; set; }
}
