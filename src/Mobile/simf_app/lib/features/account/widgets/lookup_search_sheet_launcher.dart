import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';

/// Opens the shared searchable picker sheet and returns the picked
/// [PickerOption.value], or null when the sheet is dismissed.
///
/// The nationality, birth-region and plate-letter pickers all present the same
/// beige type-to-filter sheet, and each used to hand-roll this identical
/// `showModalBottomSheet` call. One launcher keeps the three visually
/// identical — the shape, the beige fill and the scroll behaviour are decided
/// here rather than at each call site.
Future<String?> showLookupSearchSheet({
  required BuildContext context,
  required List<PickerOption> options,
  required String searchHint,
  Key? searchFieldKey,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    backgroundColor: SimfTokens.cardBeige,
    shape: const RoundedRectangleBorder(
      borderRadius:
          BorderRadius.vertical(top: Radius.circular(SimfTokens.radiusLarge)),
    ),
    builder: (_) => LookupSearchSheet(
      options: options,
      searchHint: searchHint,
      searchFieldKey: searchFieldKey,
    ),
  );
}
