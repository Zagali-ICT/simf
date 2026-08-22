import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/utils/saudi_time.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';

/// The non-widget half of the sign-up profile step's pickers.

/// The registrant must be at least 18 and at most 120 years old.
({DateTime earliest, DateTime latest}) visitorDateOfBirthRange(
  DateTime now,
) =>
    (
      earliest: DateTime(now.year - 120),
      latest: DateTime(now.year - 18, now.month, now.day),
    );

/// Where the picker opens: [current] pulled inside [range], else the newest
/// eligible date. `showDatePicker` ASSERTS `initialDate` is within its bounds,
/// and [current] comes from the server profile, which is never range-checked.
DateTime visitorDateOfBirthSeed({
  required DateTime? current,
  required ({DateTime earliest, DateTime latest}) range,
}) {
  if (current == null || current.isAfter(range.latest)) {
    return range.latest;
  }
  if (current.isBefore(range.earliest)) {
    return range.earliest;
  }
  return current;
}

/// Opens the OS date picker inside the eligible range; null if the user backs
/// out. [now] defaults to the Saudi wall clock, not the device (D-219/D-770).
Future<DateTime?> pickVisitorDateOfBirth(
  BuildContext context, {
  required DateTime? current,
  DateTime? now,
}) {
  final range = visitorDateOfBirthRange(now ?? saudiNow());
  return showDatePicker(
    context: context,
    initialDate: visitorDateOfBirthSeed(current: current, range: range),
    firstDate: range.earliest,
    lastDate: range.latest,
  );
}

/// The picked ID scan, or null if the user cancelled OR the gallery failed —
/// deliberately indistinguishable; the required-ID gate on Next reports a miss.
Future<({Uint8List bytes, String name})?> pickIdImageFromGallery() async {
  try {
    final file = await ImagePicker().pickImage(source: ImageSource.gallery);
    if (file == null) {
      return null;
    }
    return (bytes: await file.readAsBytes(), name: file.name);
  } on Object catch (_) {
    return null;
  }
}

/// Opens the searchable country sheet; the picked ISO code, or null if
/// dismissed. The caller derives the document and mobile fields (D-373).
Future<String?> pickVisitorNationality(
  BuildContext context, {
  required List<CountryItem> countries,
  required AppL10n l10n,
}) {
  return showLookupSearchSheet(
    context: context,
    options: countryPickerOptions(countries, isArabic: l10n.isArabic),
    searchHint: l10n.searchCountryHint,
    searchFieldKey: const ValueKey<String>('countrySearchField'),
  );
}
