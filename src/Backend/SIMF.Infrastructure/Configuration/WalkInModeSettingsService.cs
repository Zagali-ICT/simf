// Tests: SIMF.Api.Tests/WalkInModeSettingsTests.cs (an override wins over
//        configuration in BOTH directions, a cleared override hands the mode
//        back, and neither toggle can activate a mode while the master switch
//        is disarmed)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Common.Options;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>Resolves the two walk-in desk modes from deployment configuration
/// with a Control Panel override on top, and writes that override.
///
/// <para>The precedence is the whole point and is pinned by tests: an explicit
/// "true"/"false" row in <c>SystemSettings</c> wins, and ANY other state —
/// missing row, blank value, unparseable text — defers to
/// <c>WalkInModeOptions</c>. A fresh database therefore behaves exactly as the
/// estate configured it, which is the state every deployment starts in.</para>
///
/// <para>The master switch stays out of reach: both modes resolve as
/// <c>IsArmed(now) &amp;&amp; flag</c>, so an admin can turn a mode off during an event
/// but cannot arm walk-in registration on an estate that never enabled it.</para></summary>
internal sealed class WalkInModeSettingsService(
    SimfAppDbContext db,
    IOptionsMonitor<WalkInModeOptions> options,
    IAuditLog auditLog,
    TimeProvider timeProvider) : IWalkInModeSettings
{
    public async Task<bool> QuickRegisterActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesAsync(cancellationToken);
        var current = options.CurrentValue;
        return current.IsArmed(timeProvider.SimfNow())
            && Effective(overrides, WalkInModeSettingKeys.QuickRegister, current.QuickRegister);
    }

    public async Task<bool> AutoApproveActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesAsync(cancellationToken);
        var current = options.CurrentValue;
        return current.IsArmed(timeProvider.SimfNow())
            && Effective(overrides, WalkInModeSettingKeys.AutoApprove, current.AutoApprove);
    }

    public async Task<WalkInModeSettingsResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesAsync(cancellationToken);
        return Describe(overrides);
    }

    public async Task<WalkInModeSettingsResponse> SaveAsync(
        Guid actorUserId, AdminUpdateWalkInModeRequest request,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(
            actorUserId, WalkInModeSettingKeys.QuickRegister, request.QuickRegister,
            "Walk-in quick register (CP override; blank defers to configuration).",
            cancellationToken);
        await ApplyAsync(
            actorUserId, WalkInModeSettingKeys.AutoApprove, request.AutoApprove,
            "Walk-in auto-approve (CP override; blank defers to configuration).",
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        // One audit line carrying BOTH values, because the pair is what an
        // operator actually changed and a SOC reader correlating a burst of
        // desk approvals needs to see them together.
        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminWalkInModeChanged,
            actorUserId,
            $"quickRegister={Describe(request.QuickRegister)}; "
            + $"autoApprove={Describe(request.AutoApprove)}",
            cancellationToken);

        return await GetAsync(cancellationToken);

        static string Describe(bool? value) =>
            value is null ? "cleared" : value.Value.ToString().ToLowerInvariant();
    }

    private async Task ApplyAsync(
        Guid actorUserId, string key, bool? value, string description,
        CancellationToken cancellationToken)
    {
        var row = await db.SystemSettings
            .SingleOrDefaultAsync(s => s.Key == key, cancellationToken);
        var now = timeProvider.SimfNow();

        // Clearing writes a BLANK value rather than deleting the row. The row is
        // the CP grid's menu entry for this key, and deleting it would take the
        // knob off the page as well as out of effect.
        var stored = value is null ? string.Empty : value.Value.ToString().ToLowerInvariant();

        if (row is null)
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid(),
                Key = key,
                Value = stored,
                Description = description,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = actorUserId,
            });
            return;
        }

        row.Value = stored;
        row.IsActive = true;
        row.UpdatedAt = now;
        row.UpdatedBy = actorUserId;
    }

    private async Task<Dictionary<string, string>> LoadOverridesAsync(
        CancellationToken cancellationToken) =>
        await db.SystemSettings.AsNoTracking()
            .Where(s => s.IsActive && WalkInModeSettingKeys.All.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

    private WalkInModeSettingsResponse Describe(Dictionary<string, string> overrides)
    {
        var current = options.CurrentValue;
        var quick = Override(overrides, WalkInModeSettingKeys.QuickRegister);
        var auto = Override(overrides, WalkInModeSettingKeys.AutoApprove);

        return new WalkInModeSettingsResponse(
            Armed: current.IsArmed(timeProvider.SimfNow()),
            QuickRegister: quick ?? current.QuickRegister,
            AutoApprove: auto ?? current.AutoApprove,
            QuickRegisterConfigured: current.QuickRegister,
            AutoApproveConfigured: current.AutoApprove,
            QuickRegisterOverridden: quick is not null,
            AutoApproveOverridden: auto is not null);
    }

    private static bool Effective(
        Dictionary<string, string> overrides, string key, bool configured) =>
        Override(overrides, key) ?? configured;

    private static bool? Override(Dictionary<string, string> overrides, string key) =>
        overrides.TryGetValue(key, out var value)
            ? WalkInModeSettingKeys.ParseOverride(value)
            : null;
}
