import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:local_auth/error_codes.dart' as auth_error;
import 'package:local_auth/local_auth.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_confirm_dialog.dart';
import '../../core/motion/motion_durations.dart';

/// Maps a non-success [LocalAuthOutcome] to a localized message, so the sign-in
/// prompt and the enrol step-up share one mapping (D-738). Returns null for
/// [LocalAuthOutcome.cancelled]/[LocalAuthOutcome.success] — the caller decides
/// whether a user cancel is silent (sign-in) or shows its own copy (enrol).
/// A top-level function (not a [BiometricAuth] method) so test fakes needn't
/// implement it.
String? localizedBiometricError(AppL10n l10n, LocalAuthOutcome outcome) {
  switch (outcome) {
    case LocalAuthOutcome.lockedOut:
      return l10n.biometricLockedOut;
    case LocalAuthOutcome.noDeviceCredential:
      return l10n.biometricNoDeviceCredential;
    case LocalAuthOutcome.unavailable:
      return l10n.biometricUnavailable;
    case LocalAuthOutcome.cancelled:
    case LocalAuthOutcome.success:
      return null;
  }
}

/// The result of an OS device-credential / biometric confirmation (D-738).
enum LocalAuthOutcome {
  /// The user proved the device credential (biometric or PIN/pattern/passcode).
  success,

  /// The user dismissed / failed the prompt without proving identity.
  cancelled,

  /// The device has no screen lock set — biometric sign-in can't be secured.
  noDeviceCredential,

  /// Too many failed attempts — authentication is temporarily/permanently locked.
  lockedOut,

  /// No biometric hardware / OS support, or an unexpected platform failure.
  unavailable,
}

/// One home for the device's biometric (Face-ID / fingerprint) sign-in: the OS
/// capability check + prompt (`local_auth`) plus the device-key lifecycle
/// (query / revoke) on [AuthController]. The sign-in button, the side-menu
/// toggle, the post-sign-in nudge AND the enrolment step-up all go through this,
/// so the `local_auth` + device-key wiring has a single owner (D-441).
///
/// Enrolment (D-738, banking-standard): the toggle/nudge route to
/// [BiometricStepUpScreen], which verifies an emailed step-up code AND then
/// requires an OS device-credential confirmation ([confirmDeviceIdentity])
/// before the device key is registered — email possession + device custody,
/// like a bank app.
class BiometricAuth {
  BiometricAuth(this._ref, {LocalAuthentication? localAuth})
      : _localAuth = localAuth ?? LocalAuthentication();

  final Ref _ref;
  final LocalAuthentication _localAuth;

  AuthController get _auth => _ref.read(authControllerProvider.notifier);

  /// Runs the OS device-credential / biometric sheet ([reason] shown on it) and
  /// maps the result to a [LocalAuthOutcome]. `biometricOnly` is false so the
  /// sheet offers the device PIN/pattern/passcode as a fallback (the banking
  /// standard, and what un-bricks the post-lockout path). Used by BOTH the
  /// enrolment confirm and the sign-in prompt so the mapping lives in one place.
  Future<LocalAuthOutcome> confirmDeviceIdentity(String reason) async {
    try {
      final ok = await _localAuth.authenticate(
        localizedReason: reason,
        options: const AuthenticationOptions(
          biometricOnly: true,
          stickyAuth: true,
        ),
      );

      return ok ? LocalAuthOutcome.success : LocalAuthOutcome.cancelled;
    } on PlatformException catch (e) {
      switch (e.code) {
        case auth_error.passcodeNotSet:
        case auth_error.notEnrolled:
          return LocalAuthOutcome.noDeviceCredential;
        case auth_error.lockedOut:
        case auth_error.permanentlyLockedOut:
          return LocalAuthOutcome.lockedOut;
        default:
          return LocalAuthOutcome.unavailable;
      }
    } catch (_) {
      return LocalAuthOutcome.unavailable;
    }
  }

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
  // D-666 — a not-yet-approved account presents as guest (effectiveAppRole).
  // Skip the Face-ID nudge for a guest: it has nothing to secure yet, and
  // returning here also skips the platform probes below that would otherwise
  // stall the guest's route home (the caller awaits this before navigating).
  final auth = ref.read(authControllerProvider);
  final role = auth is AuthStateSignedIn
      ? auth.session.user.effectiveAppRole
      : AppRole.guest;
  if (role == AppRole.guest) {
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
        duration: TimeoutPolicy.biometricPrompt,
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
        style: const TextStyle(color: SimfTokens.surface),
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
