import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/features/account/data/region_models.dart';
import 'package:simf_app/features/account/data/region_repository.dart';
import 'package:simf_app/features/account/saudi_regions.dart';
import 'package:simf_app/features/account/widgets/lookup_search_sheet.dart';
import 'package:simf_app/features/account/widgets/place_of_birth_field.dart';

/// A single birth-location region for the picker, unifying the API [RegionItem]
/// (D-547) and the const [SaudiRegion] fallback behind one display shape so the
/// picker + label rendering is identical whatever the source. Mirrors
/// `SaudiRegion.name(isArabic:)`.
@immutable
class BirthRegionOption {
  const BirthRegionOption({
    required this.code,
    required this.english,
    required this.arabic,
  });

  final String code;
  final String english;
  final String arabic;

  String name({required bool isArabic}) => isArabic ? arabic : english;
}

/// D-547 — the active regions for the birth-location picker. Owner decision:
/// the data SOURCE is `GET /app/regions` (the seeded 13 regions), with the
/// const [saudiRegions] kept as an OFFLINE FALLBACK. On [AsyncData] with a
/// non-empty list the API regions win; on loading / error / empty the const
/// list is used so the picker never throws on build.
///
/// `ref.read`, not `watch`: this runs from a build AND from the picker's async
/// handler. The screen owns the `watch` so the field still rebuilds on data.
List<BirthRegionOption> activeBirthRegions(WidgetRef ref) {
  final api = ref.read(regionsProvider).asData?.value;
  if (api != null && api.isNotEmpty) {
    return <BirthRegionOption>[
      for (final RegionItem r in api)
        BirthRegionOption(
          code: r.code,
          // English uses name ?? nameArabic, mirroring SaudiRegion.name.
          english: r.name ?? r.nameArabic,
          arabic: r.nameArabic,
        ),
    ];
  }
  return <BirthRegionOption>[
    for (final SaudiRegion r in saudiRegions)
      BirthRegionOption(code: r.code, english: r.english, arabic: r.arabic),
  ];
}

/// The active region matching [code] (API or fallback), or null.
BirthRegionOption? birthRegionByCode(WidgetRef ref, String? code) {
  if (code == null) {
    return null;
  }
  for (final r in activeBirthRegions(ref)) {
    if (r.code == code) {
      return r;
    }
  }
  return null;
}

/// مكان الميلاد on the sign-up profile step, with its region picker attached.
///
/// A Saudi registrant picks from the region lookup (D-469/D-470/D-547) and the
/// picked region's localized name is what lands in [controller]; everyone else
/// types the passport's free-text place. The screen keeps the region **code**
/// so it can re-read the name when the language is toggled, which is why the
/// pick is reported back rather than stored here.
class SignUpVisitorPlaceOfBirthField extends ConsumerWidget {
  const SignUpVisitorPlaceOfBirthField({
    required this.l10n,
    required this.isSaudi,
    required this.controller,
    required this.regionCode,
    required this.showError,
    required this.onRegionPicked,
    super.key,
  });

  final AppL10n l10n;
  final bool isSaudi;
  final TextEditingController controller;

  /// The picked region's code, or null when none is set.
  final String? regionCode;

  /// True once a blocked Next has flagged an unpicked region.
  final bool showError;

  /// Reports the picked region's code and its name in the active locale.
  final void Function(String code, String name) onRegionPicked;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isArabic = l10n.isArabic;
    final regionName = regionCode == null
        ? null
        : birthRegionByCode(ref, regionCode)?.name(isArabic: isArabic);
    return PlaceOfBirthField(
      l10n: l10n,
      isSaudi: isSaudi,
      controller: controller,
      regionDisplayName: regionName,
      hasRegion: regionCode != null,
      showRegionError: showError,
      onPickRegion: () => unawaited(_pickRegion(context, ref, isArabic)),
    );
  }

  Future<void> _pickRegion(
    BuildContext context,
    WidgetRef ref,
    bool isArabic,
  ) async {
    final regions = activeBirthRegions(ref);
    final pickedCode = await showLookupSearchSheet(
      context: context,
      options: <PickerOption>[
        for (final BirthRegionOption r in regions)
          PickerOption(
            value: r.code,
            label: r.name(isArabic: isArabic),
            search: '${r.arabic} ${r.english}',
          ),
      ],
      searchHint: l10n.placeOfBirthRegionHint,
      searchFieldKey: const ValueKey<String>('birthRegionSearchField'),
    );
    if (pickedCode == null || !context.mounted) {
      return;
    }
    final picked = birthRegionByCode(ref, pickedCode);
    onRegionPicked(pickedCode, picked?.name(isArabic: isArabic) ?? '');
  }
}
