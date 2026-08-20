/// D-469 — the 13 official Saudi administrative regions, for the birth-location
/// field: a Saudi registrant picks a region from a dropdown; everyone else
/// types their place of birth free-form "as in passport". Mirrors the server
/// `SaudiRegions` (no shared runtime). The selected region's localized name is
/// stored in the existing free-text `placeOfBirth` field — no schema change.
library;

/// One region: a stable [code] plus its [arabic] / [english] display names.
class SaudiRegion {
  const SaudiRegion(this.code, this.arabic, this.english);
  final String code;
  final String arabic;
  final String english;

  /// The display name for the current locale.
  String name({required bool isArabic}) => isArabic ? arabic : english;
}

/// The 13 regions, in the conventional order.
const List<SaudiRegion> saudiRegions = <SaudiRegion>[
  SaudiRegion('riyadh', 'منطقة الرياض', 'Riyadh'),
  SaudiRegion('makkah', 'منطقة مكة المكرمة', 'Makkah'),
  SaudiRegion('madinah', 'منطقة المدينة المنورة', 'Al Madinah'),
  SaudiRegion('eastern', 'المنطقة الشرقية', 'Eastern Province'),
  SaudiRegion('asir', 'منطقة عسير', 'Asir'),
  SaudiRegion('tabuk', 'منطقة تبوك', 'Tabuk'),
  SaudiRegion('hail', 'منطقة حائل', 'Hail'),
  SaudiRegion('northern', 'منطقة الحدود الشمالية', 'Northern Borders'),
  SaudiRegion('jazan', 'منطقة جازان', 'Jazan'),
  SaudiRegion('najran', 'منطقة نجران', 'Najran'),
  SaudiRegion('bahah', 'منطقة الباحة', 'Al Bahah'),
  SaudiRegion('jawf', 'منطقة الجوف', 'Al Jawf'),
  SaudiRegion('qassim', 'منطقة القصيم', 'Al Qassim'),
];

/// The region whose Arabic or English name equals [name] (trimmed), or null —
/// used to map a stored place-of-birth string back to a dropdown selection.
SaudiRegion? regionByName(String? name) {
  final n = name?.trim() ?? '';
  if (n.isEmpty) {
    return null;
  }
  for (final r in saudiRegions) {
    if (r.arabic == n || r.english == n) {
      return r;
    }
  }
  return null;
}
