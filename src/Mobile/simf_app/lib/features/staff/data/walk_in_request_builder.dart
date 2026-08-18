import 'package:simf_app/features/account/data/app_gender.dart';
import 'package:simf_app/features/staff/data/staff_models.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form_fields.dart';
import 'package:simf_app/features/visitor_profile/data/visitor_profile_validators.dart';

/// Turns the filled walk-in form into the frozen [StaffWalkInRequest] (D-219 —
/// the JSON keys live on the model and must not move).
///
/// The routing is the point of the function: whether the identity number is
/// sent as `nationalId`, `iqamaNumber` or `passportNumber`, and the mobile as
/// `saudiMobile` or `internationalMobile`, depends on the picked nationality
/// and document type. The ones that do not apply are sent null.
///
/// [isSaudi] travels beside [nationalityCode] because the request carries both.
/// The display name is the English name, falling back to the Arabic one when
/// that is all the operator captured.
StaffWalkInRequest buildStaffWalkInRequest({
  required RegisterVisitorFormFields fields,
  required String profileTypeId,
  required String nationalityCode,
  required bool isSaudi,
  required AppGender gender,
  required VisitorDocType docType,
  required String? organisationId,
}) {
  final englishName = fields.englishName.text.trim();
  final arabicName = fields.arabicName.text.trim();
  return StaffWalkInRequest(
    displayName: englishName.isNotEmpty ? englishName : arabicName,
    arabicName: arabicName,
    englishName: englishName,
    profileTypeId: profileTypeId,
    nationalityCode: nationalityCode,
    isSaudi: isSaudi,
    gender: gender,
    email: _emptyToNull(fields.email.text),
    jobTitle: _emptyToNull(fields.jobTitle.text),
    jobTitleArabic: _emptyToNull(fields.jobTitleArabic.text),
    organisationId: organisationId,
    nationalId: isSaudi ? _emptyToNull(fields.nationalId.text) : null,
    iqamaNumber: !isSaudi && docType == VisitorDocType.iqama
        ? _emptyToNull(fields.documentNumber.text)
        : null,
    passportNumber: !isSaudi && docType == VisitorDocType.passport
        ? _emptyToNull(fields.documentNumber.text)
        : null,
    saudiMobile: isSaudi ? _emptyToNull(fields.phone.text) : null,
    internationalMobile: !isSaudi ? _emptyToNull(fields.phone.text) : null,
  );
}

String? _emptyToNull(String value) {
  final trimmed = value.trim();
  return trimmed.isEmpty ? null : trimmed;
}
