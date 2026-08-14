namespace SIMF.Common.Options;

/// <summary>
/// #7a — settings for biometric (Face-ID) device-key enrolment, bound from the
/// <c>DeviceKey</c> configuration section. When
/// <see cref="RequireStepUpForEnrol"/> is on (the default), registering a new
/// device key requires a fresh emailed-OTP step-up so a borrowed-but-unlocked
/// phone can't silently bind a credential without also holding the account's
/// email. The flag exists so the gate can be disabled in tests that exercise
/// the registration crypto/path on its own.
/// </summary>
public sealed class DeviceKeyOptions
{
    public const string SectionName = "DeviceKey";

    /// <summary>Require a valid emailed-OTP step-up code on device-key
    /// registration. Defaults to <c>true</c> (secure by default).</summary>
    public bool RequireStepUpForEnrol { get; set; } = true;

    /// <summary>
    /// How many device keys one account may hold at once. Enrolling past the cap
    /// revokes the oldest rather than refusing, so a user who replaces a phone is
    /// never stuck. Five covers a phone, a tablet and a spare while keeping the
    /// persistence surface bounded: every key is a permanent alternative
    /// credential. Zero or less disables the cap.
    /// </summary>
    public int MaxActiveKeysPerUser { get; set; } = 5;
}
