/// The lookup rows behind the profile form's pickers — country (E3),
/// profile type (E4), interest (E5) and organisation (E6). Each is one row of
/// a `GET /app/account/...` list, decoded tolerantly like the profile itself.
library;

import 'package:flutter/foundation.dart';

/// Country picker row — `GET /app/account/user-profile/countries` (E3).
@immutable
class CountryItem {
  const CountryItem({
    required this.code,
    required this.name,
    required this.nameArabic,
  });

  factory CountryItem.fromJson(Map<String, dynamic> json) => CountryItem(
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
      );

  final String code;
  final String name;
  final String nameArabic;
}

/// Profile-type picker row — `GET /app/account/profile-types` (E4).
@immutable
class ProfileTypeItem {
  const ProfileTypeItem({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.isVisitor,
    this.pageColor,
  });

  factory ProfileTypeItem.fromJson(Map<String, dynamic> json) =>
      ProfileTypeItem(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        pageColor: json['pageColor'] as String?,
        isVisitor: json['isVisitor'] as bool? ?? true,
      );

  final String id;
  final String name;
  final String nameArabic;
  final String? pageColor;
  final bool isVisitor;
}

/// Interest picker row — `GET /app/account/interests` (E5).
@immutable
class InterestItem {
  const InterestItem({
    required this.id,
    required this.name,
    required this.nameArabic,
    required this.displayOrder,
  });

  factory InterestItem.fromJson(Map<String, dynamic> json) => InterestItem(
        id: json['id'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        displayOrder: (json['displayOrder'] as num?)?.toInt() ?? 0,
      );

  final String id;
  final String name;
  final String nameArabic;
  final int displayOrder;
}

/// Organisation typeahead row — `GET /app/organisations?search=&top=` (E6).
/// Note the wire names are `nameAr` / `nameEn` (this record really uses them).
@immutable
class OrganisationItem {
  const OrganisationItem({
    required this.id,
    required this.nameAr,
    this.nameEn,
    this.city,
  });

  factory OrganisationItem.fromJson(Map<String, dynamic> json) =>
      OrganisationItem(
        id: json['id'] as String? ?? '',
        nameAr: json['nameAr'] as String? ?? '',
        nameEn: json['nameEn'] as String?,
        city: json['city'] as String?,
      );

  final String id;
  final String nameAr;
  final String? nameEn;
  final String? city;
}
