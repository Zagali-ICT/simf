namespace SIMF.Common.Enums;

/// <summary>D-717 (item 7, FDS-013 §15 GAP-3) — which decision a single-use
/// speaker action token authorises. The action is baked into the token (the token
/// is bound to one request AND one action, §15.7), so a leaked Approve link can
/// never Reject and vice versa.</summary>
public enum MeetingActionType
{
    Approve = 0,
    Reject = 1,
}
