import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/myarea/widgets/delete_account_tile.dart';

/// "Delete my account", for any screen that holds an account with no route to
/// My Area.
///
/// Apple rejected this app twice under 5.1.1(v). Both times deletion existed
/// and both times the reviewer was somewhere it could not be reached: an
/// account exists from sign-up onward, but the app holds it on the
/// profile-completion form, the interests step, the registration-success page
/// or the approval-status page, none of which carry the bottom nav. Deletion
/// has to travel with the account, not with the dashboard.
///
/// `test/repo/account_deletion_reachable_test.dart` fails the build if one of
/// those screens loses it again.
class AccountDeletionFooter extends StatelessWidget {
  const AccountDeletionFooter({super.key});

  @override
  Widget build(BuildContext context) {
    return const Padding(
      padding: EdgeInsets.only(top: SimfTokens.space3),
      child: DeleteAccountTile(),
    );
  }
}
