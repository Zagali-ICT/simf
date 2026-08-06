namespace SIMF.Common.Enums;

/// <summary>Which decision a single-use
/// speaker action token authorises. The action is baked into the token, which is
/// bound to one request AND one action, so a leaked Approve link can
/// never Reject and vice versa.</summary>
public enum MeetingActionType
{
    Approve = 0,
    Reject = 1,
}
