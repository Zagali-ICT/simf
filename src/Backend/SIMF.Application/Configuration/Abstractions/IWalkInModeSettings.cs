using SIMF.Contracts.Configuration;

namespace SIMF.Application.Configuration.Abstractions;

/// <summary>The effective walk-in mode: deployment configuration, with an
/// explicit Control Panel override on top of the two per-mode flags.
///
/// <para>Every runtime reader of <c>QuickRegisterActive</c> / <c>AutoApproveActive</c>
/// goes through here rather than reading <c>IOptionsMonitor&lt;WalkInModeOptions&gt;</c>
/// directly, because a flag read from options alone would silently ignore the
/// admin's toggle. Readers of the OTHER options — badge activation, session
/// walk-in, the arrival grace — are unaffected and still read the monitor.</para></summary>
public interface IWalkInModeSettings
{
    /// <summary>True when the reduced desk field set is live right now: the mode
    /// is armed in configuration AND quick register is on, by override if one is
    /// set and by configuration otherwise.</summary>
    Task<bool> QuickRegisterActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>True when an on-site audience registration is approved at the
    /// desk instead of queueing, under the same armed-AND-on rule.</summary>
    Task<bool> AutoApproveActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The whole picture for the CP page: effective values, what
    /// configuration alone says, and which of the two an admin has overridden.</summary>
    Task<WalkInModeSettingsResponse> GetAsync(
        CancellationToken cancellationToken = default);

    /// <summary>What a desk form needs to decide which fields it must demand:
    /// whether the reduced field set is live, and whether that floor includes an
    /// identity document.</summary>
    Task<WalkInDeskModeResponse> GetDeskAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Writes or clears the two overrides. A null field clears, handing
    /// that mode back to configuration.</summary>
    Task<WalkInModeSettingsResponse> SaveAsync(
        Guid actorUserId, AdminUpdateWalkInModeRequest request,
        CancellationToken cancellationToken = default);
}
