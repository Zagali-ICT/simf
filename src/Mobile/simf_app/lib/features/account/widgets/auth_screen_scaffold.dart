import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_auth_sweep.dart';
import 'package:simf_app/features/account/widgets/account_sub_header.dart';

/// The navy auth chrome shared by every account sub-screen (D-659): the navy
/// surface, the optional decorative sweep behind it, the [AccountSubHeader],
/// the scrolling body that fills the space under it, and whatever the screen
/// pins along the bottom.
///
/// [bottom] is a list rather than a single widget because the screens genuinely
/// differ there: the password screens pin one padded CTA, while the OTP screens
/// put a resend row OUTSIDE that padding as a further column child.
class AuthScreenScaffold extends StatelessWidget {
  const AuthScreenScaffold({
    required this.title,
    required this.onBack,
    required this.busy,
    required this.body,
    required this.bottom,
    this.sweep = false,
    super.key,
  });

  final String title;
  final VoidCallback onBack;
  final bool busy;
  final Widget body;
  final List<Widget> bottom;

  /// Paints the decorative sweep behind the content — the OTP frames have it,
  /// the password frames do not.
  final bool sweep;

  @override
  Widget build(BuildContext context) {
    final content = SafeArea(
      child: Column(
        children: <Widget>[
          AccountSubHeader(title: title, onBack: onBack, busy: busy),
          Expanded(child: body),
          ...bottom,
        ],
      ),
    );
    return Scaffold(
      backgroundColor: SimfTokens.navySurface,
      body: sweep
          ? Stack(
              children: <Widget>[
                const SimfAuthSweep(top: -180, left: null, right: -80),
                content,
              ],
            )
          : content,
    );
  }
}
