/// Wire model for the Saudi administrative-region lookup (D-547).
///
/// Split out of the repository file: a repository is the
/// transport, and the DTO is the contract. JSON keys are the
/// shipped wire contract (D-219) and are unchanged by the move.
library;

import 'package:flutter/foundation.dart';
/// Region picker row — `GET /app/regions` (D-547). [name] (English) is nullable;
/// [nameArabic] is always present. Wire keys: `code`, `name`, `nameArabic`.
@immutable
class RegionItem {
  const RegionItem({
    required this.code,
    required this.name,
    required this.nameArabic,
  });

  factory RegionItem.fromJson(Map<String, dynamic> json) => RegionItem(
        code: json['code'] as String? ?? '',
        name: json['name'] as String?,
        nameArabic: json['nameArabic'] as String? ?? '',
      );

  final String code;
  final String? name;
  final String nameArabic;
}
