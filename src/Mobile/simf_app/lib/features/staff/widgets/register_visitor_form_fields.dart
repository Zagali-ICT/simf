import 'package:flutter/material.dart';

/// The walk-in form's inputs, built once by the screen's State and handed to
/// `RegisterVisitorForm` as a single argument: one controller per text field,
/// the scroll anchor each field is reached by, and the validator bound to it.
///
/// The validators arrive as constructor arguments because they read the
/// screen's live state — the picked nationality, the document type, the last
/// 400's field rejections — which only the State knows.
class RegisterVisitorFormFields {
  RegisterVisitorFormFields({
    required this.validateArabicName,
    required this.validateEnglishName,
    required this.validateEmail,
    required this.validateJobTitle,
    required this.validateJobTitleArabic,
    required this.validateNationalId,
    required this.validateDocumentNumber,
    required this.validatePhone,
  });

  final TextEditingController email = TextEditingController();
  final TextEditingController arabicName = TextEditingController();
  final TextEditingController englishName = TextEditingController();
  final TextEditingController jobTitle = TextEditingController();
  final TextEditingController jobTitleArabic = TextEditingController();
  final TextEditingController phone = TextEditingController();
  final TextEditingController nationalId = TextEditingController();
  final TextEditingController documentNumber = TextEditingController();

  // 19l — scroll anchors, in visual order, so a blocked submit can bring the
  // FIRST problem into view instead of leaving the operator at the bottom of a
  // long form with every error off-screen above.
  final GlobalKey profileTypeAnchor = GlobalKey();
  final GlobalKey arabicNameAnchor = GlobalKey();
  final GlobalKey englishNameAnchor = GlobalKey();
  final GlobalKey nationalityAnchor = GlobalKey();
  final GlobalKey documentAnchor = GlobalKey();
  final GlobalKey documentNumberAnchor = GlobalKey();
  final GlobalKey jobTitleAnchor = GlobalKey();
  final GlobalKey phoneAnchor = GlobalKey();
  final GlobalKey organisationAnchor = GlobalKey();

  final FormFieldValidator<String> validateArabicName;
  final FormFieldValidator<String> validateEnglishName;

  /// Email and the Arabic job title carry no client-side rule of their own —
  /// they only surface what the server rejected. They still get a validator,
  /// because its presence is what puts the field on
  /// [AutovalidateMode.onUserInteraction].
  final FormFieldValidator<String> validateEmail;
  final FormFieldValidator<String> validateJobTitle;
  final FormFieldValidator<String> validateJobTitleArabic;
  final FormFieldValidator<String> validateNationalId;
  final FormFieldValidator<String> validateDocumentNumber;
  final FormFieldValidator<String> validatePhone;

  /// Empties every controller. Lives here rather than in the screen because
  /// this object owns them: a controller added above and forgotten in the
  /// screen's reset is a field that silently survives a submit.
  void clear() {
    email.clear();
    arabicName.clear();
    englishName.clear();
    jobTitle.clear();
    jobTitleArabic.clear();
    phone.clear();
    nationalId.clear();
    documentNumber.clear();
  }

  void dispose() {
    email.dispose();
    arabicName.dispose();
    englishName.dispose();
    jobTitle.dispose();
    jobTitleArabic.dispose();
    phone.dispose();
    nationalId.dispose();
    documentNumber.dispose();
  }
}
