import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/password_validation.dart';

/// The inline list of password-policy rules the typed password does not yet
/// meet, under the new-password field. Renders nothing while [unmet] is empty,
/// so a screen can pass an empty list to keep the errors hidden until the user
/// has actually typed.
class PasswordRequirementErrors extends StatelessWidget {
  const PasswordRequirementErrors({required this.unmet, super.key});

  final List<PasswordRequirement> unmet;

  @override
  Widget build(BuildContext context) {
    if (unmet.isEmpty) {
      return const SizedBox.shrink();
    }
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space2),
        for (final PasswordRequirement req in unmet)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(
              _message(req, l10n),
              style: SimfTokens.labelDangerSm,
            ),
          ),
      ],
    );
  }
}

String _message(PasswordRequirement req, AppL10n l10n) {
  switch (req) {
    case PasswordRequirement.length:
      return l10n.passwordLength;
    case PasswordRequirement.uppercase:
      return l10n.passwordUppercase;
    case PasswordRequirement.lowercase:
      return l10n.passwordLowercase;
    case PasswordRequirement.digit:
      return l10n.passwordDigit;
    case PasswordRequirement.special:
      return l10n.passwordSpecial;
  }
}
