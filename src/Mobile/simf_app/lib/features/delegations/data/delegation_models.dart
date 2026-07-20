import 'package:flutter/foundation.dart';

/// One delegation card — an invited country with its head of delegation, date
/// range and member count (Figma 1426:10771 الوفود), mirroring
/// `SIMF.Contracts.Delegations.AppDelegationItem`. Bilingual country + head
/// names are paired (the UI picks per locale); the head fields are null when no
/// head of delegation has been designated for the country.
@immutable
class DelegationItem {
  const DelegationItem({
    required this.countryId,
    required this.countryCode,
    required this.countryName,
    required this.countryNameArabic,
    required this.memberCount,
    this.headName,
    this.headNameArabic,
    this.headTitle,
    this.headTitleArabic,
    this.arrivalDate,
    this.departureDate,
  });

  final int countryId;
  final String countryCode;
  final String countryName;
  final String countryNameArabic;
  final int memberCount;
  final String? headName;
  final String? headNameArabic;
  final String? headTitle;
  final String? headTitleArabic;
  final DateTime? arrivalDate;
  final DateTime? departureDate;

  String localizedCountry(bool isArabic) =>
      _pickRequired(countryNameArabic, countryName, isArabic);

  /// The country name in the *other* language (shown as the card's subtitle).
  String localizedCountrySubtitle(bool isArabic) =>
      isArabic ? countryName.trim() : countryNameArabic.trim();

  String? localizedHead(bool isArabic) =>
      _pickOptional(headNameArabic, headName, isArabic);

  /// The head-of-delegation's title/rank in the active locale (owner 2026-07-19).
  String? localizedHeadTitle(bool isArabic) =>
      _pickOptional(headTitleArabic, headTitle, isArabic);

  bool get hasHead =>
      (headName != null && headName!.trim().isNotEmpty) ||
      (headNameArabic != null && headNameArabic!.trim().isNotEmpty);

  bool get hasDateRange => arrivalDate != null || departureDate != null;

  /// The flag emoji for [countryCode] (a regional-indicator pair). Renders as a
  /// flag on iOS; Android (no flag-emoji font) shows the two boxed letters — an
  /// informative graceful degradation either way.
  String get flagEmoji => _flagEmoji(countryCode);

  /// The single leading character of the head's localized name, for the avatar
  /// square. Empty when there is no head.
  String headInitial(bool isArabic) {
    final name = localizedHead(isArabic)?.trim() ?? '';
    if (name.isEmpty) {
      return '';
    }
    return String.fromCharCode(name.runes.first).toUpperCase();
  }

  /// Matches a free-text search over the country (both languages + code) and
  /// the head's name.
  bool matches(String query) {
    final q = query.trim().toLowerCase();
    if (q.isEmpty) {
      return true;
    }
    return countryName.toLowerCase().contains(q) ||
        countryNameArabic.toLowerCase().contains(q) ||
        countryCode.toLowerCase().contains(q) ||
        (headName?.toLowerCase().contains(q) ?? false) ||
        (headNameArabic?.toLowerCase().contains(q) ?? false);
  }

  static DelegationItem fromJson(Map<String, dynamic> json) => DelegationItem(
        countryId: (json['countryId'] as num?)?.toInt() ?? 0,
        countryCode: json['countryCode'] as String? ?? '',
        countryName: json['countryName'] as String? ?? '',
        countryNameArabic: json['countryNameArabic'] as String? ?? '',
        memberCount: (json['memberCount'] as num?)?.toInt() ?? 0,
        headName: json['headName'] as String?,
        headNameArabic: json['headNameArabic'] as String?,
        headTitle: json['headTitle'] as String?,
        headTitleArabic: json['headTitleArabic'] as String?,
        arrivalDate: _parseDate(json['arrivalDate']),
        departureDate: _parseDate(json['departureDate']),
      );
}

/// The delegations payload (`AppDelegations = { countryCount, totalParticipants,
/// items: [...] }`) — the two header stats + the per-country cards.
@immutable
class Delegations {
  const Delegations({
    required this.countryCount,
    required this.totalParticipants,
    required this.items,
  });

  final int countryCount;
  final int totalParticipants;
  final List<DelegationItem> items;

  static Delegations fromData(Object? data) {
    final map = data is Map ? data : const <dynamic, dynamic>{};
    final list = (map['items'] as List?) ?? const <dynamic>[];
    final items = list
        .whereType<Map<dynamic, dynamic>>()
        .map((e) => DelegationItem.fromJson(e.cast<String, dynamic>()))
        .toList(growable: false);
    return Delegations(
      countryCount: (map['countryCount'] as num?)?.toInt() ?? items.length,
      totalParticipants: (map['totalParticipants'] as num?)?.toInt() ?? 0,
      items: items,
    );
  }
}

DateTime? _parseDate(Object? value) =>
    value is String && value.isNotEmpty ? DateTime.tryParse(value) : null;

/// Converts a two-letter ISO 3166-1 alpha-2 code to its flag emoji (the two
/// regional-indicator code points). Returns '' for a malformed code.
String _flagEmoji(String code) {
  final upper = code.trim().toUpperCase();
  if (upper.length != 2) {
    return '';
  }
  const base = 0x1F1E6; // 🇦 — the first regional-indicator symbol.
  final first = upper.codeUnitAt(0) - 0x41;
  final second = upper.codeUnitAt(1) - 0x41;
  if (first < 0 || first > 25 || second < 0 || second > 25) {
    return '';
  }
  return String.fromCharCode(base + first) + String.fromCharCode(base + second);
}

String _pickRequired(String arabic, String english, bool isArabic) {
  final ar = arabic.trim();
  final en = english.trim();
  return isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
}

String? _pickOptional(String? arabic, String? english, bool isArabic) {
  final ar = arabic?.trim() ?? '';
  final en = english?.trim() ?? '';
  final value = isArabic ? (ar.isNotEmpty ? ar : en) : (en.isNotEmpty ? en : ar);
  return value.isEmpty ? null : value;
}
