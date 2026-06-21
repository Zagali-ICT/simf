import 'package:flutter/foundation.dart';

/// The venue-map node kind — mirrors `SIMF.Common.Enums.VenueMapNodeKind`
/// (frozen: Hall=0, Zone=1, Booth=2, PointOfInterest=3). [fromJson] decodes
/// tolerantly — the wire value is an int today, but an unknown / string value
/// resolves to [pointOfInterest] (a generic marker) rather than throwing (D-219).
enum VenueMapNodeKind {
  hall(0, 'Hall'),
  zone(1, 'Zone'),
  booth(2, 'Booth'),
  pointOfInterest(3, 'PointOfInterest');

  const VenueMapNodeKind(this.wireValue, this.wireName);

  final int wireValue;
  final String wireName;

  static VenueMapNodeKind fromJson(Object? value) {
    if (value is String) {
      for (final kind in values) {
        if (kind.wireName == value) {
          return kind;
        }
      }
    } else if (value is num) {
      final asInt = value.toInt();
      for (final kind in values) {
        if (kind.wireValue == asInt) {
          return kind;
        }
      }
    }
    return VenueMapNodeKind.pointOfInterest;
  }
}

/// One positioned node on the 2D venue map — mirrors
/// `SIMF.Contracts.Programme.PublicVenueMapNode` (`GET /app/venue-map`). `x`/`y`
/// are in the map's own design space (Page_015 L-4); a Booth node carries a
/// `boothId` that matches a [BoothSummary].
@immutable
class VenueMapNode {
  const VenueMapNode({
    required this.id,
    required this.label,
    required this.labelArabic,
    required this.kind,
    required this.x,
    required this.y,
    this.hallId,
    this.boothId,
  });

  final String id;
  final String label;
  final String labelArabic;
  final VenueMapNodeKind kind;
  final double x;
  final double y;
  final String? hallId;
  final String? boothId;

  bool get isBooth => kind == VenueMapNodeKind.booth;

  String localizedLabel(bool isArabic) {
    final ar = labelArabic.trim();
    final en = label.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  static VenueMapNode fromJson(Map<String, dynamic> json) => VenueMapNode(
        id: json['id'] as String? ?? '',
        label: json['label'] as String? ?? '',
        labelArabic: json['labelArabic'] as String? ?? '',
        kind: VenueMapNodeKind.fromJson(json['kind']),
        x: (json['x'] as num?)?.toDouble() ?? 0,
        y: (json['y'] as num?)?.toDouble() ?? 0,
        hallId: json['hallId'] as String?,
        boothId: json['boothId'] as String?,
      );
}

/// A booth summary — mirrors `SIMF.Contracts.Exhibition.PublicBoothSummary`
/// (`GET /app/booths`). Note the wire field names are `name` / `nameArabic` /
/// `exhibitorName` / `sector` (NOT `nameEn`/`nameAr`); an earlier draft of
/// Page_015_API.md named them wrong — corrected with this build. No `logoUrl`
/// and no hall **name** ship — only a bare `hallId` (D11 / Page_015 L-6).
@immutable
class BoothSummary {
  const BoothSummary({
    required this.id,
    required this.code,
    required this.name,
    required this.nameArabic,
    this.exhibitorName,
    this.exhibitorNameArabic,
    this.sector,
    this.sectorArabic,
    this.hallId,
    this.hallName,
    this.hallNameArabic,
    this.officerName,
    this.officerPhone,
    this.officerEmail,
    this.exhibitorContactId,
    this.countryId,
    this.countryName,
    this.countryNameArabic,
  });

  final String id;
  final String code;
  final String name;
  final String nameArabic;
  final String? exhibitorName;
  final String? exhibitorNameArabic;
  final String? sector;
  final String? sectorArabic;
  final String? hallId;
  // D-432 — the hall display name + booth-officer contact now ship on the wire
  // (server resolves the officer Contact-first, falling back to inline columns).
  final String? hallName;
  final String? hallNameArabic;
  final String? officerName;
  final String? officerPhone;
  final String? officerEmail;

  // P6 — D-440: the exhibitor's Contact id, the owner of the CompanyLogo asset.
  // The card renders the real logo via {base}/app/assets/CompanyLogo/{this}/image
  // (D-357), falling back to initials when null (no linked exhibitor / logo).
  final String? exhibitorContactId;

  // D-456 — the exhibitor company's country (ISO 3166-1 numeric) for the corner
  // flag on the booth logo. Null when the exhibitor has no linked Contact/country.
  final int? countryId;

  // #9 — the exhibitor company's country NAME (resolved server-side from the
  // Country lookup), shown beside the flag so the booth shows its country.
  final String? countryName;
  final String? countryNameArabic;

  String? localizedCountry(bool isArabic) =>
      _pick(countryNameArabic, countryName, isArabic);

  String localizedName(bool isArabic) {
    final ar = nameArabic.trim();
    final en = name.trim();
    return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  }

  String? localizedExhibitor(bool isArabic) =>
      _pick(exhibitorNameArabic, exhibitorName, isArabic);

  String? localizedSector(bool isArabic) =>
      _pick(sectorArabic, sector, isArabic);

  String? localizedHallName(bool isArabic) =>
      _pick(hallNameArabic, hallName, isArabic);

  static BoothSummary fromJson(Map<String, dynamic> json) => BoothSummary(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        exhibitorName: json['exhibitorName'] as String?,
        exhibitorNameArabic: json['exhibitorNameArabic'] as String?,
        sector: json['sector'] as String?,
        sectorArabic: json['sectorArabic'] as String?,
        hallId: json['hallId'] as String?,
        hallName: json['hallName'] as String?,
        hallNameArabic: json['hallNameArabic'] as String?,
        officerName: json['officerName'] as String?,
        officerPhone: json['officerPhone'] as String?,
        officerEmail: json['officerEmail'] as String?,
        exhibitorContactId: json['exhibitorContactId'] as String?,
        countryId: (json['countryId'] as num?)?.toInt(),
        countryName: json['countryName'] as String?,
        countryNameArabic: json['countryNameArabic'] as String?,
      );
}

/// A booth detail — the summary plus the description paragraph
/// (`GET /app/booths/{id}` → `PublicBoothDetail`, Page_015 E3).
@immutable
class BoothDetail {
  const BoothDetail({
    required this.id,
    required this.code,
    required this.name,
    required this.nameArabic,
    this.exhibitorName,
    this.exhibitorNameArabic,
    this.sector,
    this.sectorArabic,
    this.description,
    this.descriptionArabic,
    this.hallName,
    this.hallNameArabic,
    this.officerName,
    this.officerPhone,
    this.officerEmail,
  });

  final String id;
  final String code;
  final String name;
  final String nameArabic;
  final String? exhibitorName;
  final String? exhibitorNameArabic;
  final String? sector;
  final String? sectorArabic;
  final String? description;
  final String? descriptionArabic;
  // D-432 — see BoothSummary.
  final String? hallName;
  final String? hallNameArabic;
  final String? officerName;
  final String? officerPhone;
  final String? officerEmail;

  String? localizedDescription(bool isArabic) =>
      _pick(descriptionArabic, description, isArabic);

  String? localizedHallName(bool isArabic) =>
      _pick(hallNameArabic, hallName, isArabic);

  static BoothDetail fromJson(Map<String, dynamic> json) => BoothDetail(
        id: json['id'] as String? ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        exhibitorName: json['exhibitorName'] as String?,
        exhibitorNameArabic: json['exhibitorNameArabic'] as String?,
        sector: json['sector'] as String?,
        sectorArabic: json['sectorArabic'] as String?,
        description: json['description'] as String?,
        descriptionArabic: json['descriptionArabic'] as String?,
        hallName: json['hallName'] as String?,
        hallNameArabic: json['hallNameArabic'] as String?,
        officerName: json['officerName'] as String?,
        officerPhone: json['officerPhone'] as String?,
        officerEmail: json['officerEmail'] as String?,
      );
}

/// Picks the locale-appropriate value of a bilingual pair, falling back to the
/// other language, then to null when both are empty.
String? _pick(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
