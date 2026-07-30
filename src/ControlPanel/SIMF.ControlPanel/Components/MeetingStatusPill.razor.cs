using Microsoft.AspNetCore.Components;
using SIMF.Common.Enums;

namespace SIMF.ControlPanel.Components;

/// <summary>R10 (D-767) — the shared meeting-lifecycle status chip for the
/// speaker + delegation review desks. Maps a MeetingRequestStatus to a DISTINCT
/// SimfPill variant + a shared localized label, replacing the duplicated
/// per-page switch.</summary>
public partial class MeetingStatusPill
{
    [Parameter, EditorRequired] public MeetingRequestStatus Status { get; set; }

    // Distinct variant per state (SimfPill: neutral / admin / on / off / warn / danger).
    private string Variant => Status switch
    {
        MeetingRequestStatus.Pending => "warn",
        MeetingRequestStatus.AwaitingSpeaker => "admin",
        MeetingRequestStatus.Accepted => "on",
        MeetingRequestStatus.Done => "neutral",
        MeetingRequestStatus.Rejected => "danger",
        MeetingRequestStatus.Cancelled => "off",
        _ => "neutral",
    };

    private string LabelKey => Status switch
    {
        MeetingRequestStatus.Pending => "Admin.Meetings.Status.Pending",
        MeetingRequestStatus.AwaitingSpeaker => "Admin.Meetings.Status.AwaitingConfirmation",
        MeetingRequestStatus.Accepted => "Admin.Meetings.Status.Accepted",
        MeetingRequestStatus.Done => "Admin.Meetings.Status.Done",
        MeetingRequestStatus.Rejected => "Admin.Meetings.Status.Rejected",
        MeetingRequestStatus.Cancelled => "Admin.Meetings.Status.Cancelled",
        _ => "Admin.Meetings.Status.Pending",
    };
}
