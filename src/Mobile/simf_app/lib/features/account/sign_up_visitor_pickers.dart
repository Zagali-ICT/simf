import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/data/sign_up_visitor_lookups.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';

/// The non-widget half of the sign-up profile step's pickers.
///
/// The screen keeps the `setState` and the `mounted` checks; the rules and the
/// platform round-trips live here, beside `SignUpVisitorForm` and
/// `sign_up_visitor_submit.dart`. Same reason those two exist: the screen is
/// held under 400 lines by moving what is NOT a widget out of it, not by
/// shredding the one `_build*` method (CLAUDE.md section 1).

/// The registrant must be at least 18 and at most 120 years old.
///
/// A rule, not a layout choice, which is why it is out of `build()`'s reach:
/// `latest` is the 18th birthday and `earliest` the 120th, both derived from
/// [now] so a test can pin the boundary without waiting a year.
({DateTime earliest, DateTime latest}) visitorDateOfBirthRange(
  DateTime now,
) =>
    (
      earliest: DateTime(now.year - 120),
      latest: DateTime(now.year - 18, now.month, now.day),
    );

/// Opens the OS date picker inside the eligible range, seeded at [current] or
/// at the newest eligible date. Returns null when the user backs out.
Future<DateTime?> pickVisitorDateOfBirth(
  BuildContext context, {
  required DateTime? current,
  DateTime? now,
}) {
  final range = visitorDateOfBirthRange(now ?? DateTime.now());
  return showDatePicker(
    context: context,
    initialDate: current ?? range.latest,
    firstDate: range.earliest,
    lastDate: range.latest,
  );
}

/// The picked ID scan, or null if the user cancelled OR the gallery is
/// unavailable — the two are deliberately indistinguishable here, because the
/// screen answers both the same way and the required-ID gate on Next is what
/// reports a missing image.
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

/// Opens the searchable country sheet and returns the picked ISO code, or null
/// if the user dismissed it. The document and mobile fields are derived from
/// the result by the caller (D-373).
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
