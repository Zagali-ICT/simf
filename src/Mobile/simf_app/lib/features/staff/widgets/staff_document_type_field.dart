import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/features/account/widgets/beige_tabs.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// Iqama / passport segmented picker — the non-Saudi branch of the walk-in
/// form's document field.
class StaffDocumentTypeField extends StatelessWidget {
  const StaffDocumentTypeField({
    required this.docType,
    required this.onChanged,
    super.key,
  });

  final VisitorDocType docType;
  final ValueChanged<VisitorDocType> onChanged;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(l10n.documentTypeLabel),
        const SizedBox(height: SimfTokens.space2),
        Semantics(
          label: l10n.documentTypeLabel,
          child: BeigeTabs(
            options: <String>[l10n.iqamaSegment, l10n.passportSegment],
            selectedIndex: docType == VisitorDocType.iqama ? 0 : 1,
            onChanged: (index) => onChanged(
              index == 0 ? VisitorDocType.iqama : VisitorDocType.passport,
            ),
          ),
        ),
      ],
    );
  }
}
