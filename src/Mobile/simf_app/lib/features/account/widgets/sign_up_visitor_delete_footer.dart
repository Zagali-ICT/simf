import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/myarea/widgets/delete_account_tile.dart';

/// The "delete my account" footer under the profile-completion form.
///
/// Apple rejected build 24 under 5.1.1(v) for offering account creation
/// with no way to initiate deletion. There was one, in My Area - but
/// `splash_controller` and `routeAfterAuth` both send an account whose
/// `profileComplete` is false back to that form on every launch, so anyone
/// who registers and stops at the identity step never reaches My Area. The
/// account exists by then, so the way out has to exist where they are held.
class SignUpVisitorDeleteFooter extends StatelessWidget {
  const SignUpVisitorDeleteFooter({super.key});

  @override
  Widget build(BuildContext context) {
    return const Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SizedBox(height: SimfTokens.space3),
        DeleteAccountTile(),
      ],
    );
  }
}
