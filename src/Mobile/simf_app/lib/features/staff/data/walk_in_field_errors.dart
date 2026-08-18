import 'package:flutter/widgets.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// A field the SERVER rejected, with the exact value it rejected — so the
/// message shows on that field and clears the moment the operator edits it
/// (DEF-STF-003).
@immutable
class WalkInFieldRejection {
  const WalkInFieldRejection({
    required this.message,
    required this.rejectedValue,
  });

  final String message;
  final String rejectedValue;
}

/// The walk-in form's server-side field rejections from the last 400, keyed by
/// the request property name FluentValidation reports (DEF-STF-003).
///
/// Pure data-shaping over an [ApiFailure]: it turns the failure's details into
/// per-field messages and hands each one back only while the field still holds
/// the value the server rejected. Nothing here paints — the screen owns the
/// `setState` — which is what lets the walk-in validators consult it without
/// reaching into the screen's State.
class WalkInFieldErrors {
  final Map<String, WalkInFieldRejection> _byProperty =
      <String, WalkInFieldRejection>{};

  /// Moves [failure]'s field-level rejections onto the matching inputs so a 400
  /// highlights the offending field instead of only raising a toast. Anything
  /// the form has no field for (e.g. a whole-request rule) is left to the
  /// caller's toast.
  ///
  /// Returns true when at least one rejection landed, i.e. there is something
  /// new for the caller to paint.
  bool absorb(
    ApiFailure failure, {
    required AppL10n l10n,
    required RegisterVisitorFormFields fields,
  }) {
    final absorbed = <String, WalkInFieldRejection>{};
    for (final detail in failure.details) {
      final property = detail.field.trim();
      final controller = _controllerFor(fields, property);
      if (property.isEmpty || controller == null) {
        continue;
      }
      final message = l10n.isArabic && detail.messageArabic.trim().isNotEmpty
          ? detail.messageArabic
          : detail.message;
      if (message.trim().isEmpty) {
        continue;
      }
      absorbed[property] = WalkInFieldRejection(
        message: message,
        // Captured now, not read later: it is the comparison in [messageFor]
        // that makes editing the field clear the message.
        rejectedValue: controller.text.trim(),
      );
    }
    if (absorbed.isEmpty) {
      return false;
    }
    _byProperty.addAll(absorbed);
    return true;
  }

  /// A fresh submit re-asks the server; the last round's rejections are stale.
  void clear() => _byProperty.clear();

  /// The server's message for [property], while the field still holds the value
  /// the server rejected. Editing the field clears it without any listener.
  String? messageFor(String property, String? value) {
    final rejection = _byProperty[property];
    if (rejection == null) {
      return null;
    }
    return (value?.trim() ?? '') == rejection.rejectedValue
        ? rejection.message
        : null;
  }
}

/// The input backing a server property name, or null when the form has no field
/// for it.
TextEditingController? _controllerFor(
  RegisterVisitorFormFields fields,
  String property,
) {
  switch (property) {
    case 'ArabicName':
      return fields.arabicName;
    case 'EnglishName':
    case 'DisplayName':
      return fields.englishName;
    case 'Email':
      return fields.email;
    case 'JobTitle':
      return fields.jobTitle;
    case 'JobTitleArabic':
      return fields.jobTitleArabic;
    case 'NationalId':
      return fields.nationalId;
    case 'IqamaNumber':
    case 'PassportNumber':
      return fields.documentNumber;
    case 'SaudiMobile':
    case 'InternationalMobile':
      return fields.phone;
    default:
      return null;
  }
}
