import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_confirm_dialog.dart';

/// One home for the device's biometric (Face-ID / fingerprint) sign-in: the OS
/// capability check (`local_auth`) plus the device-key lifecycle (query / revoke)
/// on [AuthController]. The sign-in button, the side-menu toggle and the
/// post-sign-in nudge all go through this, so the `local_auth` + device-key
/// wiring has a single owner (D-441). #7a — enrolment is no longer a one-tap
/// action here: both the toggle and the nudge route to [BiometricStepUpScreen],
/// which verifies an emailed step-up code before the device key is registered.
class BiometricAuth {
  BiometricAuth(this._ref);

  final Ref _ref;
  final LocalAuthentication _localAuth = LocalAuthentication();

  AuthController get _auth => _ref.read(authControllerProvider.notifier);

  /// Whether the OS has a usable biometric (hardware + an enrolled face/finger).
  /// Errors degrade to "unavailable". A stalled platform call leaves the future
  /// pending (the side-menu tile stays hidden) — callers that block on this
  /// (the post-sign-in prompt) wrap it in a timeout so routing is never stuck.
  Future<bool> isAvailable() async {
    try {
      if (!await _localAuth.isDeviceSupported()) {
        return false;
      }
      final available = await _localAuth.getAvailableBiometrics();
      return available.isNotEmpty;
    } catch (_) {
      return false;
    }
  }

  /// Whether a device key is enrolled on this device (Face-ID sign-in is on).
  /// A secure-storage read failure degrades to "not enabled".
  Future<bool> isEnabled() async {
    try {
      return await _auth.hasEnrolledDeviceKey();
    } catch (_) {
      return false;
    }
  }

  /// Turns Face-ID sign-in off (revoke the server key + clear the local key).
  Future<void> disable() => _auth.disableDeviceKey();
}

final biometricAuthProvider =
    Provider<BiometricAuth>((ref) => BiometricAuth(ref));

/// The device biometric capability (auto-disposed; re-read after a toggle).
final biometricAvailableProvider = FutureProvider.autoDispose<bool>(
  (ref) => ref.read(biometricAuthProvider).isAvailable(),
);

/// Whether Face-ID sign-in is currently enabled on this device.
final biometricEnabledProvider = FutureProvider.autoDispose<bool>(
  (ref) => ref.read(biometricAuthProvider).isEnabled(),
);

/// Caps the capability/storage probe in the post-sign-in nudge so a stalled
/// platform call can never wedge the route home (it runs while the sign-in
/// button spinner is up).
const Duration _kBiometricProbeTimeout = Duration(seconds: 2);

/// Post-sign-in nudge (D-441; D-445): if the device can do biometrics and the
/// user hasn't enabled Face-ID yet, show a **notification-style** prompt — a
/// SnackBar with an Enable action — at **every** login until it is activated
/// (owner D-445; was a one-time modal). Called by BOTH the password sign-in and
/// the OTP completion, so every sign-in path can activate Face-ID. A no-op when
/// biometrics are unavailable or Face-ID is already on.
Future<void> maybeOfferBiometricEnrolment(
  BuildContext context,
  WidgetRef ref,
) async {
  final biometric = ref.read(biometricAuthProvider);
  // Bounded so a stalled platform probe can never wedge the route home.
  final enabled = await biometric
      .isEnabled()
      .timeout(_kBiometricProbeTimeout, onTimeout: () => false);
  if (enabled) {
    return;
  }
  final available = await biometric
      .isAvailable()
      .timeout(_kBiometricProbeTimeout, onTimeout: () => false);
  if (!available || !context.mounted) {
    return;
  }
  final l10n = AppL10n.of(context);
  // The root MaterialApp messenger carries the SnackBar across the route change
  // to the destination, so the nudge is visible on the screen the user lands on.
  final messenger = ScaffoldMessenger.of(context);
  // The Enable action fires up to 8s later — after the route change that pops
  // the sign-in / OTP screen — so capture the lifetime-safe ProviderContainer +
  // the GoRouter now, NOT the screen's WidgetRef / context (defunct by then).
  final container = ProviderScope.containerOf(context, listen: false);
  final router = GoRouter.of(context);
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        content: Text(l10n.biometricPromptBody),
        duration: const Duration(seconds: 8),
        action: SnackBarAction(
          label: l10n.biometricPromptEnable,
          // #7a — the nudge tap is the confirmation; route to the step-up
          // screen, which emails + verifies the code and enrols. Refresh the
          // toggle's state when the user returns.
          onPressed: () {
            messenger.hideCurrentSnackBar();
            unawaited(
              router
                  .pushNamed(RouteNames.biometricStepUp)
                  .then((_) => container.invalidate(biometricEnabledProvider)),
            );
          },
        ),
      ),
    );
}

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
    return SwitchListTile(
      secondary: const Icon(Icons.fingerprint, color: SimfTokens.accent),
      title: Text(
        l10n.biometricEnableToggle,
        style: const TextStyle(color: Colors.white),
      ),
      value: enabled,
      activeThumbColor: SimfTokens.accent,
      // Ignore taps while a toggle is in flight, so a double-tap can't register
      // (or revoke) two device keys and desync the local/server state.
      onChanged: _busy ? null : (turnOn) => unawaited(_toggle(l10n, turnOn)),
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

  /// Confirms the destructive disable: revoking the device key deletes the local
  /// biometric credential permanently (it can only be re-enrolled, not restored).
  /// Returns true only when the user explicitly taps Delete.
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
