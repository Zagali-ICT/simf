import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/features/account/biometric_auth.dart';
import 'package:simf_app/features/account/post_auth_route.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';

/// Runs the device-key ("Face ID") sign-in: availability, enrolment, the OS
/// prompt, then the credential exchange and the post-auth routing.
///
/// Lifted out of the sign-in screen's State, where it was 100 lines of
/// authentication with no widget code in it. [onError] and [onBusy] hand
/// results back so the caller owns its own `setState` and its own `mounted`
/// check; [context] stays a parameter because the OS prompt and the routing
/// both need one.
Future<void> runBiometricSignIn({
  required BuildContext context,
  required WidgetRef ref,
  required AppL10n l10n,
  required void Function(String? message) onError,
  required void Function({required bool busy}) onBusy,
}) async {
  final BiometricAuth biometric = ref.read(biometricAuthProvider);
  final notifier = ref.read(authControllerProvider.notifier);

  // (1) The device must have a usable biometric / secured lock.
  if (!await biometric.isAvailable()) {
    onError(l10n.biometricUnavailable);
    return;
  }
  if (!context.mounted) {
    return;
  }

  // (2) Face login needs a device key, enrolled on a prior sign-in.
  bool enrolled;
  try {
    enrolled = await notifier.hasEnrolledDeviceKey();
  } catch (_) {
    enrolled = false;
  }
  if (!enrolled) {
    onError(l10n.biometricNotEnrolled);
    return;
  }

  // (3) OS prompt - biometric-first, device-PIN fallback (D-738).
  final LocalAuthOutcome outcome =
      await biometric.confirmDeviceIdentity(l10n.biometricSignInTooltip);
  if (!context.mounted) {
    return;
  }
  if (outcome != LocalAuthOutcome.success) {
    // A cancel is the user's own choice, so it stays silent (null); other
    // outcomes get feedback, via the shared BiometricAuth mapping.
    onError(localizedBiometricError(l10n, outcome));
    return;
  }

  onBusy(busy: true);
  onError(null);
  Object? unexpectedError;
  try {
    await notifier.signInWithDeviceKey();
  } on AuthFailure catch (failure) {
    onError(failure.source.localizedMessage(l10n));
  } catch (e) {
    // Non-AuthFailure (for example a FormatException from reloadCurrentUser
    // before the defence in signInWithDeviceKey was added): captured so the
    // session routing below still happens. Shown in a SnackBar AFTER routing,
    // so the user sees the error but is not stranded on the sign-in screen.
    unexpectedError = e;
  } finally {
    onBusy(busy: false);
  }

  // Route on the resulting auth state, OUTSIDE the try: signInWithDeviceKey
  // establishes the session before its trailing profile reload, so a
  // non-AuthFailure thrown there must not skip the navigation home - the
  // biometric path mirrors the password path (D-441).
  if (context.mounted && ref.read(authControllerProvider) is AuthStateSignedIn) {
    if (unexpectedError != null) {
      // The localized generic message, NOT '$unexpectedError' - a raw Dart
      // toString is untranslated and leaks internal type detail to the user.
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.errorGenericBody)),
      );
    }
    routeAfterAuth(context, ref);
  }
}
