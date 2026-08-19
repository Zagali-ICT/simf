import 'package:flutter/material.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/widgets/profile_type_field.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_form_state.dart';

/// The الفئة (classification) slot on the sign-up profile step.
///
/// C5 (D-371) — under the **Visitor** tab there is no pick to make: the type is
/// locked to the single seeded "Normal" (عادي) row and assigned by the screen,
/// so the slot renders nothing. Under **Other** it is the full
/// [ProfileTypeField] with its D-375 loading / retry / loaded states.
///
/// The empty case stays a `SizedBox.shrink()` rather than dropping the slot:
/// the card's fixed spacers sit between the children, and removing the child
/// would close the gap the Figma frame keeps.
class SignUpVisitorProfileTypeField extends StatelessWidget {
  const SignUpVisitorProfileTypeField({
    required this.l10n,
    required this.form,
    required this.isVisitorType,
    required this.loading,
    required this.failed,
    required this.onRetry,
    required this.onChanged,
    super.key,
  });

  final AppL10n l10n;

  /// Supplies the loaded types, the current pick and the submit-attempt flag.
  final VisitorProfileFormState form;

  final bool isVisitorType;
  final bool loading;
  final bool failed;
  final VoidCallback onRetry;
  final ValueChanged<String?> onChanged;

  @override
  Widget build(BuildContext context) {
    if (isVisitorType) {
      return const SizedBox.shrink();
    }
    return ProfileTypeField(
      l10n: l10n,
      loading: loading,
      failed: failed,
      items: form.profileTypes,
      selectedId: form.profileTypeId,
      showError: form.triedSubmit && form.profileTypeId == null,
      onRetry: onRetry,
      onChanged: onChanged,
    );
  }
}
