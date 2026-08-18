import 'package:flutter/foundation.dart';
import 'package:simf_app/features/account/data/app_gender.dart' show AppGender;

/// D-509 — the staff walk-in registration request. Mirrors the backend
/// `AdminWalkInRegistrationRequest` (the same DTO the CP desk posts), trimmed
/// to the fields the staff-app form (Figma 1467:12357) collects. Email is
/// optional (the API synthesizes a placeholder); the server validator enforces
/// the full rule set, so the form does only light required-field checks.
@immutable
class StaffWalkInRequest {
  const StaffWalkInRequest({
    required this.displayName,
    required this.arabicName,
    required this.englishName,
    required this.profileTypeId,
    required this.nationalityCode,
    required this.isSaudi,
    required this.gender,
    this.email,
    this.jobTitle,
    this.jobTitleArabic,
    this.organisationId,
    this.nationalId,
    this.iqamaNumber,
    this.passportNumber,
    this.saudiMobile,
    this.internationalMobile,
  });

  final String displayName;
  final String arabicName;
  final String englishName;
  final String profileTypeId;
  final String nationalityCode;
  final bool isSaudi;
  final AppGender gender;
  final String? email;
  final String? jobTitle;
  final String? jobTitleArabic;
  final String? organisationId;
  final String? nationalId;
  final String? iqamaNumber;
  final String? passportNumber;
  final String? saudiMobile;
  final String? internationalMobile;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'displayName': displayName,
        'arabicName': arabicName,
        'englishName': englishName,
        'profileTypeId': profileTypeId,
        'nationalityCode': nationalityCode,
        'isSaudi': isSaudi,
        'gender': gender.value,
        if (email != null) 'email': email,
        if (jobTitle != null) 'jobTitle': jobTitle,
        if (jobTitleArabic != null) 'jobTitleArabic': jobTitleArabic,
        if (organisationId != null) 'organisationId': organisationId,
        if (nationalId != null) 'nationalId': nationalId,
        if (iqamaNumber != null) 'iqamaNumber': iqamaNumber,
        if (passportNumber != null) 'passportNumber': passportNumber,
        if (saudiMobile != null) 'saudiMobile': saudiMobile,
        if (internationalMobile != null)
          'internationalMobile': internationalMobile,
      };
}

/// D-509 — the walk-in result. Mirrors `AdminWalkInRegistrationResponse`; an
/// empty [qrId] is the PendingApproval state (the QR is minted on approval,
/// D-425) — the success screen treats it as "submitted for approval".
@immutable
class StaffWalkInResult {
  const StaffWalkInResult({
    required this.userId,
    required this.displayName,
    required this.qrId,
    required this.profileTypeName,
  });

  factory StaffWalkInResult.fromJson(Map<String, dynamic> json) =>
      StaffWalkInResult(
        userId: json['userId'] as String? ?? '',
        displayName: json['displayName'] as String? ?? '',
        qrId: json['qrId'] as String? ?? '',
        profileTypeName: json['profileTypeName'] as String? ?? '',
      );

  final String userId;
  final String displayName;
  final String qrId;
  final String profileTypeName;

  bool get isPending => qrId.trim().isEmpty;
}
