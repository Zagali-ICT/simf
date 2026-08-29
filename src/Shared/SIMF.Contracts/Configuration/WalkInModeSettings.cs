namespace SIMF.Contracts.Configuration;

/// <summary>The two walk-in desk modes as an admin sees them on the CP page,
/// with enough context that an inert toggle explains itself.
///
/// <para><see cref="Armed"/> is the master switch — walk-in mode enabled and
/// inside its window — and it is NOT admin-editable: it lives in deployment
/// configuration. When it reads false both modes are inactive whatever the
/// toggles say, which is exactly why it is on this payload.</para>
///
/// <para>The <c>*Configured</c> fields carry what configuration alone would give,
/// so the page can show that a toggle is currently overriding the deployed
/// posture rather than agreeing with it.</para></summary>
public sealed record WalkInModeSettingsResponse(
    bool Armed,
    bool QuickRegister,
    bool AutoApprove,
    bool QuickRegisterConfigured,
    bool AutoApproveConfigured,
    bool QuickRegisterOverridden,
    bool AutoApproveOverridden);

/// <summary>What a DESK needs to render its form, and nothing more.
///
/// <para>Separate from <see cref="WalkInModeSettingsResponse"/> on purpose: this
/// is read by whoever may register a walk-in, which is a different and much
/// larger set of people than those who may change the modes. It carries no
/// configuration-versus-override detail because a desk operator cannot act on
/// it.</para>
///
/// <para><see cref="RequiresIdentityDocument"/> is NOT admin-editable — it stays
/// in deployment configuration — but the form has to know it, because it decides
/// whether the quick floor is "a name" or "a name and one document".</para></summary>
public sealed record WalkInDeskModeResponse(
    bool QuickRegister,
    bool RequiresIdentityDocument);

/// <summary>The CP save. Each field is tri-state: true or false writes an
/// explicit override, null CLEARS it and returns that mode to whatever
/// deployment configuration says. Clearing is the reason these are nullable —
/// without it an admin could never hand a mode back to the estate's own
/// setting.</summary>
public sealed record AdminUpdateWalkInModeRequest
{
    public bool? QuickRegister { get; init; }

    public bool? AutoApprove { get; init; }
}
