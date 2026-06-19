import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';

/// The outcome of turning Face-ID sign-in on.
enum BiometricEnableResult { ok, unavailable, failed }

/// One home for the device's biometric (Face-ID / fingerprint) sign-in: the OS
/// capability check (`local_auth`) plus the device-key lifecycle (enrol /
/// revoke / query) on [AuthController]. The sign-in button, the one-time enrol
/// prompt and the side-menu toggle all go through this, so the `local_auth` +
/// device-key wiring has a single owner (D-441).
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

  /// Enrols a device key — requires a signed-in session and an OS biometric.
  Future<BiometricEnableResult> enable() async {
    if (!await isAvailable()) {
      return BiometricEnableResult.unavailable;
    }
    try {
      await _auth.enrolDeviceKey();
      return BiometricEnableResult.ok;
    } catch (_) {
      return BiometricEnableResult.failed;
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
  // the sign-in / OTP screen — so capture the lifetime-safe ProviderContainer,
  // NOT the screen's WidgetRef (which would be defunct by then).
  final container = ProviderScope.containerOf(context, listen: false);
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        content: Text(l10n.biometricPromptBody),
        duration: const Duration(seconds: 8),
        action: SnackBarAction(
          label: l10n.biometricPromptEnable,
          onPressed: () =>
              unawaited(_enableFromNudge(container, messenger, l10n)),
        ),
      ),
    );
}

/// Runs the enrol when the nudge's Enable action is tapped, then toasts the
/// outcome and refreshes the toggle's state. Uses the [ProviderContainer] (not
/// a screen [WidgetRef]) because it runs after the originating screen is gone.
Future<void> _enableFromNudge(
  ProviderContainer container,
  ScaffoldMessengerState messenger,
  AppL10n l10n,
) async {
  final result = await container.read(biometricAuthProvider).enable();
  container.invalidate(biometricEnabledProvider);
  messenger
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(biometricEnableMessage(l10n, result))));
}

/// Maps an enable outcome to its localized toast — shared by the post-sign-in
/// nudge and the toggle so the mapping has one (exhaustive) owner.
String biometricEnableMessage(AppL10n l10n, BiometricEnableResult result) {
  switch (result) {
    case BiometricEnableResult.ok:
      return l10n.biometricEnabledToast;
    case BiometricEnableResult.unavailable:
      return l10n.biometricUnavailable;
    case BiometricEnableResult.failed:
      return l10n.biometricEnableFailedToast;
  }
}

/// The Face-ID sign-in enable/disable toggle (D-441; surfaced in the side menu
/// AND the profile, D-445). Self-hides when the device has no usable biometric.
/// Enabling enrols a device key (so the sign-in screen's Face-ID button works
/// next time); disabling revokes it. Toasts the outcome. Stateful for an
/// in-flight guard so a double-tap can't register/revoke two keys.
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

  Future<void> _toggle(AppL10n l10n, bool turnOn) async {
    final messenger = ScaffoldMessenger.of(context);
    final biometric = ref.read(biometricAuthProvider);
    setState(() => _busy = true);
    try {
      final String message;
      if (turnOn) {
        message = biometricEnableMessage(l10n, await biometric.enable());
      } else {
        await biometric.disable();
        message = l10n.biometricDisabledToast;
      }
      if (!mounted) {
        return;
      }
      ref.invalidate(biometricEnabledProvider);
      messenger.showSnackBar(SnackBar(content: Text(message)));
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }
}
