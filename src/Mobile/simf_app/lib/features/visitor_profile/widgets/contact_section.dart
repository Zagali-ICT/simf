import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/widgets/mobile_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// How to reach the visitor: one mobile field whose shape follows nationality.
///
/// A Saudi registrant enters an `05XXXXXXXX` number and everyone else an
/// international one, so the CONTROLLER changes with [isSaudi] as well as the
/// validator. Both controllers are passed in rather than one being derived
/// here: the screens keep a separate controller per shape, so a visitor who
/// picks the wrong nationality and corrects it does not lose what they typed.
class ContactSection extends StatelessWidget {
  const ContactSection({
    required this.l10n,
    required this.isSaudi,
    required this.saudiMobile,
    required this.internationalMobile,
    this.callingCode = '',
    this.onPickCallingCode,
    super.key,
  });

  final AppL10n l10n;

  /// Derived from the nationality pick (D-373), never its own switch.
  final bool isSaudi;

  final TextEditingController saudiMobile;
  final TextEditingController internationalMobile;

  /// The calling code in front of the number. Defaults from the nationality,
  /// then belongs to the visitor — a Sudanese national attending on a Saudi
  /// number picks +966 and keeps their nationality.
  final String callingCode;

  /// Opens the shared searchable country sheet.
  final VoidCallback? onPickCallingCode;

  @override
  Widget build(BuildContext context) {
    return MobileField(
      saudi: isSaudi,
      controller: isSaudi ? saudiMobile : internationalMobile,
      validator: isSaudi
          ? (v) => validateSaudiMobile(v, l10n)
          : (v) => validateInternationalMobile(v, l10n),
      callingCode: callingCode,
      onPickCallingCode: onPickCallingCode,
    );
  }
}
