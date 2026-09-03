import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/route_names.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_confirm_dialog.dart';
import 'package:simf_app/core/errors/api_error_l10n.dart';
import 'package:simf_app/features/account/data/profile_repository.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// "Delete my account" — the in-app half of the deletion path Google Play
/// requires of any app that offers account creation.
///
/// Its own widget rather than a row in `MyAreaMoreSection` because a
/// destructive action needs `ref`, an in-flight guard and a confirmation, and
/// that section is a const [StatelessWidget]. Follows the FaceIdToggleTile
/// precedent, which disables a credential for the same reasons.
class DeleteAccountTile extends ConsumerStatefulWidget {
  const DeleteAccountTile({super.key});

  @override
  ConsumerState<DeleteAccountTile> createState() => _DeleteAccountTileState();
}

class _DeleteAccountTileState extends ConsumerState<DeleteAccountTile> {
  bool _busy = false;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return TextButton(
      onPressed: _busy ? null : () => _confirmAndDelete(l10n),
      style: TextButton.styleFrom(
        alignment: AlignmentDirectional.centerStart,
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space3),
      ),
      child: Text(
        l10n.deleteAccountLink,
        style: SimfTokens.bodyBeigeMd.copyWith(color: SimfTokens.danger),
      ),
    );
  }

  Future<void> _confirmAndDelete(AppL10n l10n) async {
    // Captured before every await: a token refresh can dispose this State
    // mid-flight, and the router/messenger must survive that.
    final router = GoRouter.of(context);
    final messenger = ScaffoldMessenger.of(context);
    final auth = ref.read(authControllerProvider.notifier);
    final repository = ref.read(profileRepositoryProvider);

    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.deleteAccountConfirmTitle,
      message: l10n.deleteAccountConfirmBody,
      confirmLabel: l10n.deleteAccountConfirmAction,
      isDestructive: true,
    );
    if (!confirmed) {
      return;
    }

    // The dialog above was awaited, and a token refresh can dispose this State
    // while it is open - the same reason the router and messenger are captured
    // before it. setState on a disposed State throws.
    if (!mounted) {
      return;
    }
    setState(() => _busy = true);
    try {
      await repository.deleteMyAccount();
      // Drop this device's biometric credential BEFORE signing out. signOut
      // deliberately keeps the device key so a re-open can use it, which is
      // right for a sign-out and wrong for an erasure: the sign-in screen names
      // the account a key belongs to, so leaving it would advertise the erased
      // address on a pre-auth screen and spend the user's face on a credential
      // the server already revoked.
      try {
        await auth.disableDeviceKey();
      } on Object catch (_) {
        // Best-effort, and it has two expected failures: the server revoke is
        // a call against a key deletion has already revoked, and secure
        // storage can refuse. Neither may strand the holder on this screen
        // with an error for an account the server has already erased.
      }
      // The server has already revoked the session, so this clears local state
      // rather than asking permission to end something that is already over.
      await auth.signOut();
      router.goNamed(RouteNames.signIn);
    } on ApiFailure catch (failure) {
      final message = failure.localizedMessage(l10n).trim();
      messenger.showSnackBar(
        SnackBar(
          content: Text(
            message.isEmpty ? l10n.deleteAccountFailed : message,
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }
}
