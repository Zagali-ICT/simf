import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// My Devices — أجهزتي · route: RouteNames.myDevices
/// Purpose: show every biometric device key on the account and let the owner
///   revoke any of them.
/// Data: `authControllerProvider.listDeviceKeys()` →
///   `GET /app/auth/device-keys`; revoke → `DELETE /app/auth/device-keys/{id}`.
/// Figma: none. The screen closes security finding S10, and no node exists for
///   it; the owner asked (2026-08-14) for the established house style rather
///   than a new design, so it is built from the shared `Simf*` catalogue.
/// Perf: one request, a short list (capped at five active keys server-side), so
///   a plain builder over an in-memory list with no pagination.
/// Contract: revoking THIS device's key must also clear the local private half,
///   which `AuthController.revokeDeviceKey` owns, or the app would still offer a
///   Face-ID button backed by a dead credential.
class MyDevicesScreen extends ConsumerStatefulWidget {
  const MyDevicesScreen({super.key});

  @override
  ConsumerState<MyDevicesScreen> createState() => _MyDevicesScreenState();
}

class _MyDevicesScreenState extends ConsumerState<MyDevicesScreen> {
  List<DeviceKeyEntryDto>? _devices;
  String? _localDeviceKeyId;
  String? _error;
  bool _loading = true;
  String? _busyId;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    if (mounted) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final notifier = ref.read(authControllerProvider.notifier);
      final devices = await notifier.listDeviceKeys();
      final localId = await notifier.enrolledDeviceKeyId();
      if (!mounted) {
        return;
      }
      setState(() {
        // Active first, then most recently added. A revoked row still shows,
        // because "this device was removed on the 3rd" is the useful half of an
        // audit trail the user can actually read.
        _devices = devices.toList()
          ..sort((a, b) {
            if (a.isActive != b.isActive) {
              return a.isActive ? -1 : 1;
            }
            final left = b.createdAt ?? DateTime(0);
            final right = a.createdAt ?? DateTime(0);
            return left.compareTo(right);
          });
        _localDeviceKeyId = localId;
        _loading = false;
      });
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = failure.source.localizedMessage(AppL10n.of(context));
        _loading = false;
      });
    }
  }

  Future<void> _revoke(DeviceKeyEntryDto device) async {
    final l10n = AppL10n.of(context);
    final isThisDevice = device.id == _localDeviceKeyId;
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.myDevicesRevokeTitle,
      message: isThisDevice
          ? l10n.myDevicesRevokeThisDeviceBody
          : l10n.myDevicesRevokeBody,
      confirmLabel: l10n.myDevicesRevokeConfirm,
      isDestructive: true,
    );
    if (!confirmed || !mounted) {
      return;
    }

    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busyId = device.id);
    try {
      await ref.read(authControllerProvider.notifier).revokeDeviceKey(device.id);
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.myDevicesRevokedToast)),
      );
      await _load();
    } on AuthFailure catch (failure) {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(failure.source.localizedMessage(l10n))),
      );
    } finally {
      if (mounted) {
        setState(() => _busyId = null);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.myDevicesTitle,
      showBottomNav: false,
      body: SimfPullToRefresh(
        onRefresh: _load,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading && _devices == null) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_error != null) {
      return SimfPullableHost(
        child: SimfErrorState(
          message: _error!,
          retryLabel: l10n.retryLabel,
          onRetry: _load,
        ),
      );
    }
    final devices = _devices ?? const <DeviceKeyEntryDto>[];
    if (devices.isEmpty) {
      return SimfPullableHost(
        child: SimfEmptyState(
          icon: Icons.fingerprint,
          message: l10n.myDevicesEmpty,
        ),
      );
    }
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(SimfTokens.space4),
      itemCount: devices.length,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (_, index) => _DeviceRow(
        device: devices[index],
        isThisDevice: devices[index].id == _localDeviceKeyId,
        busy: _busyId == devices[index].id,
        onRevoke: () => _revoke(devices[index]),
      ),
    );
  }
}

class _DeviceRow extends StatelessWidget {
  const _DeviceRow({
    required this.device,
    required this.isThisDevice,
    required this.busy,
    required this.onRevoke,
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
                        _Chip(text: l10n.myDevicesThisDevice),
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
                      width: 24,
                      height: 24,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
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

  /// Saudi local time, never UTC on a user-facing surface (D-770).
  String _stamp(DateTime value) {
    final local = saudiOf(value);
    final date = '${local.year}-'
        '${local.month.toString().padLeft(2, '0')}-'
        '${local.day.toString().padLeft(2, '0')}';
    return '$date ${formatSaudiTime12(local)}';
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.accent.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(text, style: SimfTokens.labelGoldMedium),
    );
  }
}
