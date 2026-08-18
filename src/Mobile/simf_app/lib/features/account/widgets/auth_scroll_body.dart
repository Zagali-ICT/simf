import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/responsive/max_width_body.dart';
import 'package:simf_app/features/account/widgets/auth_screen_scaffold.dart';

/// The scrolling body under [AuthScreenScaffold]'s header: token side padding,
/// the [MaxWidthBody] cap the screen chooses, and a column of its content.
///
/// Pass [formKey] on a screen whose content is a [Form]; the OTP screens have
/// no form fields and leave it null.
class AuthScrollBody extends StatelessWidget {
  const AuthScrollBody({
    required this.maxWidth,
    required this.children,
    this.formKey,
    this.crossAxisAlignment = CrossAxisAlignment.center,
    super.key,
  });

  final double maxWidth;
  final List<Widget> children;
  final GlobalKey<FormState>? formKey;
  final CrossAxisAlignment crossAxisAlignment;

  @override
  Widget build(BuildContext context) {
    final key = formKey;
    final column = Column(
      crossAxisAlignment: crossAxisAlignment,
      children: children,
    );
    return SingleChildScrollView(
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
      child: MaxWidthBody(
        maxWidth: maxWidth,
        child: key == null ? column : Form(key: key, child: column),
      ),
    );
  }
}
