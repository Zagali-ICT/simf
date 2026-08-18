/// The walk-in desk's pickers.
///
/// Three are lookup sheets (19j): classification, nationality and
/// organisation all present the SHARED searchable sheet, exactly like
/// Create-profile, instead of a raw Material dropdown. Each launcher only
/// decides what the rows say and which hint the search field carries; the
/// sheet's shape, fill and scroll behaviour stay with [showLookupSearchSheet].
/// Each returns the picked id, or null when the operator dismissed the sheet.
///
/// The fourth picks the visitor's document or photo off the camera or the
/// gallery (19f).
library;

import 'package:flutter/widgets.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/core/widgets/simf_image_source_sheet.dart';
import 'package:simf_app/features/account/data/profile_lookups.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/staff/data/walk_in_attachments.dart';
import 'package:simf_app/features/staff/widgets/register_visitor_form.dart';

/// 19g — the visitor-eligible classifications the operator picks from.
Future<String?> pickWalkInProfileType(
  BuildContext context,
  List<ProfileTypeItem> profileTypes,
) {
  final l10n = AppL10n.of(context);
  return showLookupSearchSheet(
    context: context,
    options: <PickerOption>[
      for (final ProfileTypeItem t in profileTypes)
        PickerOption(
          value: t.id,
          label: l10n.isArabic ? t.nameArabic : t.name,
          search: '${t.name} ${t.nameArabic}',
        ),
    ],
    searchHint: l10n.profileTypeSearchHint,
    searchFieldKey: const ValueKey<String>('staffProfileTypeSearchField'),
  );
}

/// Pops the country CODE — the wire carries the code, not the display name.
Future<String?> pickWalkInNationality(
  BuildContext context,
  List<CountryItem> countries,
) {
  final l10n = AppL10n.of(context);
  return showLookupSearchSheet(
    context: context,
    options: <PickerOption>[
      for (final CountryItem c in countries)
        PickerOption(
          value: c.code,
          label: l10n.isArabic ? c.nameArabic : c.name,
          search: '${c.name} ${c.nameArabic}',
        ),
    ],
    searchHint: l10n.searchCountryHint,
    searchFieldKey: const ValueKey<String>('staffCountrySearchField'),
  );
}

/// The rows read through [organisationDisplayName], so the selected row and the
/// option that produced it read the same.
Future<String?> pickWalkInOrganisation(
  BuildContext context,
  List<OrganisationItem> organisations,
) {
  final l10n = AppL10n.of(context);
  return showLookupSearchSheet(
    context: context,
    options: <PickerOption>[
      for (final OrganisationItem o in organisations)
        PickerOption(
          value: o.id,
          label: organisationDisplayName(o, l10n),
          search: '${o.nameAr} ${o.nameEn ?? ''}',
        ),
    ],
    searchHint: l10n.organisationSearchHint,
    searchFieldKey: const ValueKey<String>('staffOrganisationSearchField'),
  );
}

/// 19f — offers the CAMERA as well as a file pick: a registration desk must be
/// able to shoot the visitor's document without leaving the app.
///
/// Null when the operator backed out, or when the camera / gallery is
/// unavailable — the attachments are optional, so the registration still goes
/// through without them.
Future<WalkInAttachmentFile?> pickWalkInAttachment(BuildContext context) async {
  final source = await showSimfImageSourceSheet(context);
  if (source == null || !context.mounted) {
    return null;
  }
  try {
    final file = await ImagePicker().pickImage(source: source);
    if (file == null) {
      return null;
    }
    return WalkInAttachmentFile(
      bytes: await file.readAsBytes(),
      filename: file.name,
    );
  } on Exception {
    return null;
  }
}
