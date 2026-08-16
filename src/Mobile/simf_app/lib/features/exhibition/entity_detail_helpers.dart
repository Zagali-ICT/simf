/// Shared pure helpers for the entity-detail screens (exhibitor + sponsor),
/// which both render [EntityDetailScaffold]. Extracted from the byte-identical
/// copies that lived in each screen.
library;

import 'package:simf_app/core/utils/initials.dart';
import 'package:simf_app/features/exhibition/widgets/entity_detail_scaffold.dart'
    show EntityDetailScaffold;

/// Joins "City، Country" (Arabic comma in RTL); either side may be null.
String? entityLocationLine(
  String? city,
  String? country, {
  required bool isArabic,
}) {
  final parts = <String>[
    if ((city ?? '').trim().isNotEmpty) city!.trim(),
    if ((country ?? '').trim().isNotEmpty) country!.trim(),
  ];
  if (parts.isEmpty) {
    return null;
  }
  return parts.join(isArabic ? '، ' : ', ');
}

/// The first two letters of a name, upper-cased, for the logo fallback.
String entityInitials(String name) => initialsFromStart(name);

/// Parses a website into an http(s) [Uri] (prepending https:// when the scheme
/// is missing); null when blank / unparseable.
Uri? entityHttpUri(String? raw) {
  final value = (raw ?? '').trim();
  if (value.isEmpty) {
    return null;
  }
  final withScheme = value.startsWith('http://') || value.startsWith('https://')
      ? value
      : 'https://$value';
  return Uri.tryParse(withScheme);
}
