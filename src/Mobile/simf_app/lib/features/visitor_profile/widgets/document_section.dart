import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/validation/digit_normalization.dart';
import 'package:simf_app/core/validation/field_limits.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/simf_labeled_text_field.dart';
import 'package:simf_app/features/account/widgets/beige_tabs.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// The visitor's identity document, which is a different field depending on
/// nationality (D-373).
///
/// A Saudi registrant enters a national ID and nothing else. Everyone else
/// picks Iqama or passport first, and the number field then follows that choice
/// in both its length cap and its validator.
///
/// Both paths fold Arabic-Indic digits to Western through
/// [WesternDigitsFormatter], so an id typed on an Arabic keypad still submits
/// as `1XXXXXXXXX` (owner 2026-07-06). The Saudi field additionally strips
/// non-digits; the non-Saudi one does not, because a passport number contains
/// letters.
class DocumentSection extends StatelessWidget {
  const DocumentSection({
    required this.l10n,
    required this.isSaudi,
    required this.docType,
    required this.nationalId,
    required this.documentNumber,
    required this.onDocTypeChanged,
    super.key,
  });

  final AppL10n l10n;

  /// Derived from the nationality pick, never its own switch.
  final bool isSaudi;

  final VisitorDocType docType;
  final TextEditingController nationalId;
  final TextEditingController documentNumber;

  /// Switching document type CLEARS the number: an Iqama typed into a passport
  /// field is not a passport, and leaving it there would submit the wrong value
  /// under the wrong key. The caller clears its own controller so the widget
  /// never mutates state it does not own.
  final ValueChanged<VisitorDocType> onDocTypeChanged;

  /// The document number's length cap follows the document: an Iqama is 10
  /// digits, a passport up to 9 characters.
  static const int _iqamaLength = 10;
  static const int _passportLength = 9;

  @override
  Widget build(BuildContext context) {
    if (isSaudi) {
      return SimfLabeledTextField(
        label: l10n.nationalIdLabel,
        controller: nationalId,
        keyboardType: TextInputType.number,
        maxLength: FieldLimits.nationalId,
        inputFormatters: <TextInputFormatter>[
          const WesternDigitsFormatter(),
          FilteringTextInputFormatter.digitsOnly,
        ],
        validator: (v) => validateNationalId(v, l10n),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.documentTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        BeigeTabs(
          options: <String>[l10n.iqamaSegment, l10n.passportSegment],
          selectedIndex: docType == VisitorDocType.iqama ? 0 : 1,
          onChanged: (index) => onDocTypeChanged(
            index == 0 ? VisitorDocType.iqama : VisitorDocType.passport,
          ),
        ),
        const SizedBox(height: SimfTokens.space4),
        SimfLabeledTextField(
          label: l10n.documentNumberLabel,
          controller: documentNumber,
          maxLength:
              docType == VisitorDocType.iqama ? _iqamaLength : _passportLength,
          // Letters pass for passports, so no digits-only filter here.
          inputFormatters: const <TextInputFormatter>[WesternDigitsFormatter()],
          validator: (v) => validateDocumentNumber(v, l10n, docType),
        ),
      ],
    );
  }
}
