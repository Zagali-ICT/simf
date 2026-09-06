namespace SIMF.Common.Options;

/// <summary>
/// Settings for self-service account deletion, bound from the
/// <c>AccountDeletion</c> configuration section.
/// </summary>
/// <remarks>
/// <para>When <see cref="RequireCodeForDeletion"/> is on (the default), erasing
/// an account requires a fresh emailed one-time code, so a borrowed-but-unlocked
/// phone cannot destroy an account without also holding its inbox. Deletion is
/// irreversible, which is exactly why it earns the same second factor that
/// enrolling a credential does.</para>
/// <para>The flag exists so the existing deletion tests can drive the erase path
/// on its own, the same way <c>DeviceKeyOptions.RequireStepUpForEnrol</c> does
/// for enrolment. Turning it off in production would remove the confirmation
/// Apple review is shown, so it is on by default and stays on.</para>
/// </remarks>
public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";

    /// <summary>Require a valid emailed one-time code on
    /// <c>DELETE /app/account</c>. Defaults to <c>true</c> (secure by default).
    /// </summary>
    public bool RequireCodeForDeletion { get; set; } = true;
}
