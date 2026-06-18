import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';

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

/// Caps the capability/storage probe in the post-sign-in prompt so a stalled
/// platform call can never wedge the route home (it runs while the sign-in
/// button spinner is up).
const Duration _kBiometricProbeTimeout = Duration(seconds: 2);

/// One-time post-sign-in nudge (D-441): if the device can do biometrics, the
/// user hasn't enabled Face-ID yet, and the prompt hasn't been shown before,
/// offer to enable it. Called by BOTH the password sign-in and the OTP
/// completion, so every sign-in path can activate Face-ID (closing the old
/// gap where only the direct password path enrolled). A no-op when biometrics
/// are unavailable or already enabled.
Future<void> maybeOfferBiometricEnrolment(
  BuildContext context,
  WidgetRef ref,
) async {
  final prefs = ref.read(simfPrefsStorageProvider);
  if (prefs.getBool(StorageKeys.biometricPromptHandled) ?? false) {
    return;
  }
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
  final enable = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: Text(l10n.biometricPromptTitle),
      content: Text(l10n.biometricPromptBody),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: Text(l10n.biometricPromptNotNow),
        ),
        FilledButton(
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: Text(l10n.biometricPromptEnable),
        ),
      ],
    ),
  );
  if (!context.mounted) {
    return;
  }
  if (enable != true) {
    // Declined — don't nudge again (the side-menu toggle remains the way in).
    await prefs.setBool(StorageKeys.biometricPromptHandled, true);
    return;
  }
  final result = await biometric.enable();
  ref.invalidate(biometricEnabledProvider);
  // Burn the one-time nudge only on success; a transient enable failure leaves
  // it armed so the next sign-in re-offers (self-heal).
  if (result == BiometricEnableResult.ok) {
    await prefs.setBool(StorageKeys.biometricPromptHandled, true);
  }
  if (!context.mounted) {
    return;
  }
  // The root MaterialApp messenger carries the SnackBar across the route change
  // that follows, so the confirmation is visible on the destination.
  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(content: Text(biometricEnableMessage(l10n, result))),
  );
}

/// Maps an enable outcome to its localized toast — shared by the post-sign-in
/// prompt and the side-menu toggle so the mapping has one (exhaustive) owner.
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
