import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/core/utils/refresh.dart';
import 'package:simf_app/features/account/data/device_keys.dart';
import 'package:simf_app/features/account/widgets/device_row.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// My Devices — أجهزتي · route: RouteNames.myDevices · Figma: none (security
/// finding S10; owner 2026-08-14 asked for the house style, not a new design).
/// Contract: revoking THIS device's key must also clear the local private half,
/// which `AuthController.revokeDeviceKey` owns, or the app would still offer a
/// Face-ID button backed by a dead credential.
class MyDevicesScreen extends ConsumerStatefulWidget {
  const MyDevicesScreen({super.key});

  @override
  ConsumerState<MyDevicesScreen> createState() => _MyDevicesScreenState();
}

class _MyDevicesScreenState extends ConsumerState<MyDevicesScreen> {
  String? _busyId;

  Future<void> _refresh() => refreshAsync(ref, deviceKeysProvider.future);

  Future<void> _revoke(DeviceKeyEntryDto device) async {
    final l10n = AppL10n.of(context);
    final isThisDevice =
        device.id == ref.read(deviceKeysProvider).value?.localDeviceKeyId;
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
      await ref
          .read(authControllerProvider.notifier)
          .revokeDeviceKey(device.id);
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.myDevicesRevokedToast)),
      );
      await _refresh();
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
        onRefresh: _refresh,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    return ref.watch(deviceKeysProvider).when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: SimfTokens.accent),
          ),
          error: (error, _) => SimfPullableHost(
            child: SimfErrorState(
              message: error is AuthFailure
                  ? error.source.localizedMessage(l10n)
                  : l10n.errorGenericBody,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(deviceKeysProvider),
            ),
          ),
          data: (list) => _list(l10n, list),
        );
  }

  Widget _list(AppL10n l10n, DeviceKeyList list) {
    final devices = list.devices;
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
      itemBuilder: (_, index) => DeviceRow(
        device: devices[index],
        isThisDevice: devices[index].id == list.localDeviceKeyId,
        busy: _busyId == devices[index].id,
        onRevoke: () => _revoke(devices[index]),
      ),
    );
  }
}
