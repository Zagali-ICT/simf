import 'package:flutter/foundation.dart';
import 'package:simf_app/features/account/data/app_gender.dart';

/// Body for `POST /app/account/user-profile` (Page_007 E2). Actor comes from the
/// token (D7) — no user id / email. `dateOfBirth` is an ISO date `yyyy-MM-dd`.
@immutable
class UpsertUserProfileRequest {
  const UpsertUserProfileRequest({
    required this.interestIds,
    required this.arabicName,
    required this.englishName,
    required this.nationalityCode,
    required this.placeOfBirth,
    required this.isSaudi,
    required this.gender,
    this.profileTypeId,
    this.jobTitle,
    this.dateOfBirth,
    this.nationalId,
    this.iqamaNumber,
    this.passportNumber,
    this.saudiMobile,
    this.internationalMobile,
    this.plateNumber,
    this.organisationId,
    this.organisationOther,
    this.showInMeetLikeYou,
    this.regionId,
    this.jobTitleArabic,
  });

  final String? profileTypeId;
  final List<String> interestIds;
  final String arabicName;
  final String englishName;
  final String? jobTitle;
  final String nationalityCode;
  final String? dateOfBirth; // yyyy-MM-dd
  final String placeOfBirth;
  final bool isSaudi;
  final String? nationalId;
  final String? iqamaNumber;
  final String? passportNumber;
  final String? saudiMobile;
  final String? internationalMobile;

  /// C6 (D-371) — رقم اللوحة, optional; Saudi standard when filled.
  final String? plateNumber;
  final String? organisationId;

  /// Set only alongside an [organisationId] the picker flagged
  /// `isOther`; the server clears it on any ordinary pick.
  final String? organisationOther;
  final AppGender gender;

  /// D-736 — "Show in Meet People Like You" visibility toggle. Null means
  /// "no change" (preserves server's current value). Defaults to true.
  final bool? showInMeetLikeYou;

  /// D-547 — the attendee's Region (KSA region lookup). Carried on an edit
  /// so the full-profile upsert (the only write path) does not null it — the
  /// service sets RegionId unconditionally from the request.
  final String? regionId;

  /// The Arabic job title. Carried on an edit for the same reason as
  /// [regionId] (the service sets JobTitleArabic unconditionally).
  final String? jobTitleArabic;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'profileTypeId': profileTypeId,
        'interestIds': interestIds,
        'arabicName': arabicName,
        'englishName': englishName,
        'jobTitle': jobTitle,
        'nationalityCode': nationalityCode,
        'dateOfBirth': dateOfBirth,
        'placeOfBirth': placeOfBirth,
        'isSaudi': isSaudi,
        'nationalId': nationalId,
        'iqamaNumber': iqamaNumber,
        'passportNumber': passportNumber,
        'saudiMobile': saudiMobile,
        'internationalMobile': internationalMobile,
        'plateNumber': plateNumber,
        'organisationId': organisationId,
        'organisationOther': organisationOther,
        'gender': gender.value,
        'showInMeetLikeYou': showInMeetLikeYou,
        'regionId': regionId,
        'jobTitleArabic': jobTitleArabic,
      };

  /// Returns a copy with [interestIds] replaced — used to attach the interests
  /// picked on Page 007‑01 to the data collected on Page 007 before the single
  /// save (D-332).
  UpsertUserProfileRequest copyWith({List<String>? interestIds}) {
    return UpsertUserProfileRequest(
      interestIds: interestIds ?? this.interestIds,
      arabicName: arabicName,
      englishName: englishName,
      nationalityCode: nationalityCode,
      placeOfBirth: placeOfBirth,
      isSaudi: isSaudi,
      gender: gender,
      profileTypeId: profileTypeId,
      jobTitle: jobTitle,
      dateOfBirth: dateOfBirth,
      nationalId: nationalId,
      iqamaNumber: iqamaNumber,
      passportNumber: passportNumber,
      saudiMobile: saudiMobile,
      internationalMobile: internationalMobile,
      plateNumber: plateNumber,
      organisationId: organisationId,
      organisationOther: organisationOther,
      showInMeetLikeYou: showInMeetLikeYou,
      regionId: regionId,
      jobTitleArabic: jobTitleArabic,
    );
  }
}

/// Response for `GET`/`POST /app/account/user-profile` (Page_007 E1/E2). Every
/// field is empty/null on a first-time profile (except `qrId` when Approved and
/// `profileTypeId` when an admin pre-assigned one).
@immutable
class UserProfileResponse {
  const UserProfileResponse({
    required this.interestIds,
    required this.arabicName,
    required this.englishName,
    required this.nationalityCode,
    required this.placeOfBirth,
    required this.isSaudi,
    required this.gender,
    required this.hasIdImage,
    required this.hasAvatar,
    this.profileTypeId,
    this.jobTitle,
    this.dateOfBirth,
    this.nationalId,
    this.iqamaNumber,
    this.passportNumber,
    this.saudiMobile,
    this.internationalMobile,
    this.plateNumber,
    this.plateNumberAr,
    this.plateNumberEn,
    this.referenceNumber,
    this.organisationId,
    this.organisationOther,
    this.qrId,
    this.isVip = false,
    this.allowsSpeakerMeeting = false,
    this.allowsDelegationMeeting = false,
    this.showInMeetLikeYou = true,
    this.isForVisitor = true,
    this.regionId,
    this.jobTitleArabic,
  });

  factory UserProfileResponse.fromJson(Map<String, dynamic> json) {
    return UserProfileResponse(
      profileTypeId: json['profileTypeId'] as String?,
      interestIds: (json['interestIds'] as List<dynamic>? ?? const <dynamic>[])
          .map((e) => e as String)
          .toList(),
      arabicName: json['arabicName'] as String? ?? '',
      englishName: json['englishName'] as String? ?? '',
      jobTitle: json['jobTitle'] as String?,
      nationalityCode: json['nationalityCode'] as String? ?? '',
      dateOfBirth: json['dateOfBirth'] as String?,
      placeOfBirth: json['placeOfBirth'] as String? ?? '',
      isSaudi: json['isSaudi'] as bool? ?? false,
      nationalId: json['nationalId'] as String?,
      iqamaNumber: json['iqamaNumber'] as String?,
      passportNumber: json['passportNumber'] as String?,
      saudiMobile: json['saudiMobile'] as String?,
      internationalMobile: json['internationalMobile'] as String?,
      plateNumber: json['plateNumber'] as String?,
      plateNumberAr: json['plateNumberAr'] as String?,
      plateNumberEn: json['plateNumberEn'] as String?,
      referenceNumber: json['referenceNumber'] as String?,
      organisationId: json['organisationId'] as String?,
      organisationOther: json['organisationOther'] as String?,
      gender: AppGender.fromValue((json['gender'] as num?)?.toInt()),
      hasIdImage: json['hasIdImage'] as bool? ?? false,
      hasAvatar: json['hasAvatar'] as bool? ?? false,
      qrId: json['qrId'] as String?,
      isVip: json['isVip'] as bool? ?? false,
      allowsSpeakerMeeting: json['allowsSpeakerMeeting'] as bool? ?? false,
      allowsDelegationMeeting:
          json['allowsDelegationMeeting'] as bool? ?? false,
      showInMeetLikeYou: json['showInMeetLikeYou'] as bool? ?? true,
      isForVisitor: json['isForVisitor'] as bool? ?? true,
      regionId: json['regionId'] as String?,
      jobTitleArabic: json['jobTitleArabic'] as String?,
    );
  }

  final String? profileTypeId;
  final List<String> interestIds;
  final String arabicName;
  final String englishName;
  final String? jobTitle;
  final String nationalityCode;
  final String? dateOfBirth;
  final String placeOfBirth;
  final bool isSaudi;
  final String? nationalId;
  final String? iqamaNumber;
  final String? passportNumber;
  final String? saudiMobile;
  final String? internationalMobile;

  /// C6 (D-371/D-459) — رقم اللوحة, optional Saudi vehicle plate, stored as the
  /// canonical Latin code.
  final String? plateNumber;

  /// C6 (D-459) — the plate rendered in Arabic (Arabic letters + Arabic-Indic
  /// digits), derived server-side from [plateNumber].
  final String? plateNumberAr;

  /// C6 (D-459) — the plate rendered in English/Latin (the canonical code).
  final String? plateNumberEn;

  /// D-373 — the registration reference (SIMF-2026-00000001), issued once
  /// at profile creation. Customer-facing lookup key; NOT the QR id.
  final String? referenceNumber;
  final String? organisationId;

  /// The employer the user typed when theirs is not in the lookup;
  /// null for every ordinary pick.
  final String? organisationOther;
  final AppGender gender;
  final bool hasIdImage;

  /// True when a face photo (avatar) is stored. Mandatory for men, optional
  /// for women (the two-photo split). Append-only wire field (defaults false
  /// when an older server omits it).
  final bool hasAvatar;
  final String? qrId;

  /// D-729 (owner item 15) — true when the account's tier is VVIP/VIP
  /// (server-computed from ProfileType.IsVipTier). It used to gate
  /// the "request a speaker meeting" CTA; the Bi-Meeting rework (D-760) moved
  /// that to [allowsSpeakerMeeting], so **no screen reads this today**. Kept
  /// because the server still sends it and it stays part of the frozen wire
  /// contract (D-219). Append-only wire field (defaults false when an older
  /// server omits it).
  final bool isVip;

  /// Bi-Meeting rework — the admin-assigned per-user flag that lets this
  /// account request a **speaker** meeting. Replaces [isVip] as the
  /// speaker-meeting gate (independent of the VIP tier). Append-only wire field
  /// (defaults false).
  final bool allowsSpeakerMeeting;

  /// Bi-Meeting rework — the admin-assigned per-user flag that lets this
  /// account request a **delegation** (وفد) meeting. Append-only wire field
  /// (defaults false).
  final bool allowsDelegationMeeting;

  /// D-736 — whether this profile appears in "Meet People Like You"
  /// recommendations. Defaults to true.
  final bool showInMeetLikeYou;

  /// Build #13 — true when the assigned profile type is an audience tier (VVIP
  /// / VIP / Gold / Normal); false for the "Other" (partner / staff) tiers.
  /// Drives whether the "show me in Meet People Like You" opt-in is offered
  /// (only to "Other"-type users). Append-only wire field; defaults true.
  final bool isForVisitor;

  /// D-547 — the attendee's Region id. Read back so an interests-only edit can
  /// re-send it (the full-profile upsert sets RegionId unconditionally).
  final String? regionId;

  /// The Arabic job title, read back for the same reason as [regionId].
  final String? jobTitleArabic;

  /// SUPERSEDED for routing (D-374): the post-sign-in gate now reads the
  /// server-computed `profileComplete` on the session user — do NOT reuse
  /// this getter for routing; the server rule is the single authority.
  /// Kept only for the parked `_legacy_mockup` screen; mirrors the server
  /// rule: names + ≥1 interest + the ID document (all) + the face photo (men).
  bool get isComplete =>
      arabicName.trim().isNotEmpty &&
      englishName.trim().isNotEmpty &&
      interestIds.isNotEmpty &&
      hasIdImage &&
      (gender != AppGender.male || hasAvatar);

  /// Builds an edit re-save request mirroring every field of the loaded profile
  /// so an interests-only edit (via `copyWith`) re-POSTs the whole profile
  /// without nulling a server-set field. The full upsert is the only write path
  /// and the service sets RegionId + JobTitleArabic unconditionally, so both
  /// must be carried back here (#14).
  ///
  /// EXCEPTION — `profileTypeId` is sent **null** on purpose: on a re-save the
  /// server re-runs the sign-up self-pick validation for any non-null
  /// ProfileTypeId and rejects (400) every non-"Normal" tier (VVIP/VIP/partner/
  /// staff), which are admin-assigned. Sending null skips that validation, and
  /// the server's admin-wins precedence keeps the existing type untouched (it
  /// only writes a user pick when the stored ProfileTypeId is null). So an
  /// interests edit never changes — nor is blocked by — the account's tier.
  /// [showInMeetLikeYou] overrides the loaded opt-in value — Build #13 lets an
  /// "Other"-type user toggle it on the My-interests edit screen; null keeps
  /// the current value.
  ///
  /// [mobile] overrides the stored mobile number (owner 2026-07-26 — add / edit
  /// the phone number from the profile, validate only, no OTP). It is written
  /// to the field the profile's nationality selects — [saudiMobile] for a Saudi
  /// national, [internationalMobile] otherwise — so the pair never carries two
  /// numbers at once. Null keeps both stored values.
  UpsertUserProfileRequest toUpsertRequest({
    bool? showInMeetLikeYou,
    String? mobile,
  }) =>
      UpsertUserProfileRequest(
        interestIds: interestIds,
        arabicName: arabicName,
        englishName: englishName,
        nationalityCode: nationalityCode,
        placeOfBirth: placeOfBirth,
        isSaudi: isSaudi,
        gender: gender,
        jobTitle: jobTitle,
        dateOfBirth: dateOfBirth,
        nationalId: nationalId,
        iqamaNumber: iqamaNumber,
        passportNumber: passportNumber,
        saudiMobile: mobile == null || !isSaudi ? saudiMobile : mobile,
        internationalMobile:
            mobile == null || isSaudi ? internationalMobile : mobile,
        plateNumber: plateNumber,
        organisationId: organisationId,
        organisationOther: organisationOther,
        showInMeetLikeYou: showInMeetLikeYou ?? this.showInMeetLikeYou,
        regionId: regionId,
        jobTitleArabic: jobTitleArabic,
      );
}
