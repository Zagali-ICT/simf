import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/account/widgets/device_chip.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// One enrolled biometric device key on the My Devices list.
///
/// Extracted from `my_devices_screen.dart`, where it was a private `_DeviceRow`
/// class — SIMF-C3 fires on a private widget class wherever it lives, and the
/// remedy is its own file under `widgets/`.
class DeviceRow extends StatelessWidget {
  const DeviceRow({
    required this.device,
    required this.isThisDevice,
    required this.busy,
    required this.onRevoke,
    super.key,
  });

  final DeviceKeyEntryDto device;
  final bool isThisDevice;
  final bool busy;
  final VoidCallback onRevoke;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Row(
          children: <Widget>[
            Icon(
              Icons.smartphone,
              color: device.isActive
                  ? SimfTokens.accent
                  : SimfTokens.beigeBorder,
            ),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      Flexible(
                        child: Text(
                          device.label.isEmpty
                              ? l10n.myDevicesUnnamed
                              : device.label,
                          style: SimfTokens.labelWhiteBoldXl,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      if (isThisDevice) ...<Widget>[
                        const SizedBox(width: SimfTokens.space2),
                        DeviceChip(text: l10n.myDevicesThisDevice),
                      ],
                    ],
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(_subtitle(l10n), style: SimfTokens.bodyBeigeMd),
                ],
              ),
            ),
            if (device.isActive)
              busy
                  ? const SizedBox(
                      width: SimfTokens.space6,
                      height: SimfTokens.space6,
                      child: CircularProgressIndicator(
                        strokeWidth: SimfTokens.deviceRowStrokeWidth,
                        color: SimfTokens.accent,
                      ),
                    )
                  : IconButton(
                      onPressed: onRevoke,
                      tooltip: l10n.myDevicesRevokeConfirm,
                      icon: const Icon(
                        Icons.delete_outline,
                        color: SimfTokens.danger,
                      ),
                    ),
          ],
        ),
      ),
    );
  }

  /// Last-used is the line that actually tells the owner whether a device they
  /// do not recognise has been used, so it wins over "added" when both exist.
  String _subtitle(AppL10n l10n) {
    if (!device.isActive) {
      return device.revokedAt == null
          ? l10n.myDevicesRevoked
          : '${l10n.myDevicesRevoked} · ${_stamp(device.revokedAt!)}';
    }
    if (device.lastUsedAt != null) {
      return '${l10n.myDevicesLastUsed} ${_stamp(device.lastUsedAt!)}';
    }
    if (device.createdAt != null) {
      return '${l10n.myDevicesAdded} ${_stamp(device.createdAt!)}';
    }
    return l10n.myDevicesNeverUsed;
  }

  /// Saudi local time, never UTC on a user-facing surface.
  String _stamp(DateTime value) {
    final local = saudiOf(value);
    final date = '${local.year}-'
        '${local.month.toString().padLeft(2, '0')}-'
        '${local.day.toString().padLeft(2, '0')}';
    return '$date ${formatSaudiTime12(local)}';
  }
}
