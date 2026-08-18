import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/biometric_step_up_screen.dart'
    show BiometricStepUpScreen;

/// The Face-ID sign-in enable/disable toggle (D-441; surfaced in the side menu
/// AND the profile, D-445). Self-hides when the device has no usable biometric.
/// #7a — enabling confirms intent then routes to [BiometricStepUpScreen] (an
/// emailed-OTP step-up enrols the device key); disabling confirms + revokes it.
/// Stateful for an in-flight guard so a double-tap can't launch/revoke twice.
class FaceIdToggleTile extends ConsumerStatefulWidget {
  const FaceIdToggleTile({super.key});

  @override
  ConsumerState<FaceIdToggleTile> createState() => _FaceIdToggleTileState();
}

class _FaceIdToggleTileState extends ConsumerState<FaceIdToggleTile> {
  bool _busy = false;

  @override
  Widget build(BuildContext context) {
    final available =
        ref.watch(biometricAvailableProvider).valueOrNull ?? false;
    if (!available) {
      return const SizedBox.shrink();
    }
    final enabled = ref.watch(biometricEnabledProvider).valueOrNull ?? false;
    final l10n = AppL10n.of(context);
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        SwitchListTile(
          secondary: const Icon(Icons.fingerprint, color: SimfTokens.accent),
          title: Text(
            l10n.biometricEnableToggle,
            style: const TextStyle(color: SimfTokens.surface),
          ),
          value: enabled,
          activeThumbColor: SimfTokens.accent,
          // Ignore taps while a toggle is in flight, so a double-tap can't
          // register (or revoke) two device keys and desync local/server state.
          onChanged:
              _busy ? null : (turnOn) => unawaited(_toggle(l10n, turnOn)),
        ),
        // S10 — the way into My Devices. Shown whenever biometrics are
        // available rather than only when this device is enrolled, because the
        // account may hold keys from OTHER devices that this one cannot see,
        // and those are exactly the ones worth checking on.
        ListTile(
          leading: const Icon(Icons.devices_other, color: SimfTokens.accent),
          title: Text(
            l10n.myDevicesManage,
            style: const TextStyle(color: SimfTokens.surface),
          ),
          trailing: const Icon(
            Icons.chevron_right,
            color: SimfTokens.beigeBorder,
          ),
          onTap: () => unawaited(
            context.pushNamed(RouteNames.myDevices).then((_) {
              if (mounted) {
                ref.invalidate(biometricEnabledProvider);
              }
            }),
          ),
        ),
      ],
    );
  }

  Future<void> _toggle(AppL10n l10n, bool turnOn) =>
      turnOn ? _enableWithStepUp(l10n) : _disable(l10n);

  /// #7a — enabling first confirms intent, then routes to the emailed-OTP
  /// step-up screen which verifies the code and enrols the device key. The
  /// switch reflects the new state when that screen pops (it invalidates the
  /// enabled-state provider on success).
  Future<void> _enableWithStepUp(AppL10n l10n) async {
    if (!await _confirmEnable(l10n) || !mounted) {
      return;
    }
    setState(() => _busy = true);
    try {
      await context.pushNamed(RouteNames.biometricStepUp);
      if (mounted) {
        ref.invalidate(biometricEnabledProvider);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  /// Disabling permanently deletes the device key — confirm before revoking it
  /// (owner 2026-06-21), then revoke + toast.
  Future<void> _disable(AppL10n l10n) async {
    if (!await _confirmDisable(l10n) || !mounted) {
      return;
    }
    final messenger = ScaffoldMessenger.of(context);
    final biometric = ref.read(biometricAuthProvider);
    setState(() => _busy = true);
    try {
      await biometric.disable();
      if (!mounted) {
        return;
      }
      ref.invalidate(biometricEnabledProvider);
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.biometricDisabledToast)),
      );
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  /// #7a — confirms intent before starting the emailed-OTP enable flow.
  Future<bool> _confirmEnable(AppL10n l10n) {
    return SimfConfirmDialog.show(
      context,
      title: l10n.biometricEnableConfirmTitle,
      message: l10n.biometricEnableConfirmBody,
      confirmLabel: l10n.biometricEnableConfirmAction,
    );
  }

  /// Confirms the destructive disable: revoking the device key deletes the
  /// local biometric credential permanently (it can only be re-enrolled, not
  /// restored). Returns true only when the user explicitly taps Delete.
  Future<bool> _confirmDisable(AppL10n l10n) {
    return SimfConfirmDialog.show(
      context,
      title: l10n.biometricDisableConfirmTitle,
      message: l10n.biometricDisableConfirmBody,
      confirmLabel: l10n.biometricDisableConfirmAction,
      isDestructive: true,
    );
  }
}
