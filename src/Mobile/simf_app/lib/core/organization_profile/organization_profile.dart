import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../utils/event_date_range.dart';

/// Coerce a decoded JSON value into a `Map<String, dynamic>` (empty when
/// absent).
Map<String, dynamic> _asStringMap(dynamic value) =>
    (value as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};

/// Parse the calendar date out of an ISO-8601 date(-time) string (null when
/// absent / malformed). Only the `YYYY-MM-DD` head is used, so the offset never
/// shifts the day — the backend stores the event date as a midnight-elsewhere
/// `DateTimeOffset`, and the intended date is the written calendar date
/// regardless of the reader's timezone.
DateTime? _asDate(dynamic value) {
  final raw = value as String?;
  if (raw == null || raw.length < 10) {
    return null;
  }
  return DateTime.tryParse(raw.substring(0, 10));
}

/// D-495 — the public Organization / About profile (`GET
/// /app/organization-profile`):
/// the edition-generic forum config (name / title / slogan / bio, year + dates
/// +
/// status, location, contact, live-stream link, social links, logo) plus the
/// about-items + details lists. Loaded once and cached for the app lifetime;
/// consumers read `.valueOrNull` and fall back to bundled l10n while loading /
/// on
/// error (offline-safe). Editable in the Control Panel — the same build
/// re-skins to
/// any edition by editing this data.
class OrgProfile {
  const OrgProfile({
    required this.name,
    required this.nameArabic,
    required this.title,
    required this.titleArabic,
    required this.currentYear,
    required this.status,
    required this.social,
    required this.aboutItems,
    required this.details,
    this.slogan,
    this.sloganArabic,
    this.bio,
    this.bioArabic,
    this.locationText,
    this.locationTextArabic,
    this.contactPhone,
    this.contactEmail,
    this.contactWebsite,
    this.liveStreamUrl,
    this.backgroundVideoUrl,
    this.logoUrl,
    this.version,
    this.eventStartDate,
    this.eventEndDate,
  });

  final String name;
  final String nameArabic;
  final String title;
  final String titleArabic;
  final int currentYear;
  final String status;
  final OrgSocial social;
  final List<OrgAboutItem> aboutItems;
  final List<OrgDetail> details;
  final String? slogan;
  final String? sloganArabic;
  final String? bio;
  final String? bioArabic;
  final String? locationText;
  final String? locationTextArabic;
  final String? contactPhone;
  final String? contactEmail;
  final String? contactWebsite;
  final String? liveStreamUrl;

  /// The hero-section background video link (a YouTube URL or a direct MP4/HLS
  /// link), set in the CP Organization Profile. Played muted + looping behind
  /// the
  /// home hero (#43 / D-756); null falls back to the banner image / discover
  /// photo.
  final String? backgroundVideoUrl;

  final String? logoUrl;
  final String? version;

  /// The forum edition's start / end dates (#43 surfaces them on the home hero
  /// via [eventDateRange]). Nullable — the config may not have set them yet.
  final DateTime? eventStartDate;
  final DateTime? eventEndDate;

  String nameFor(bool isArabic) => isArabic ? nameArabic : name;
  String titleFor(bool isArabic) => isArabic ? titleArabic : title;
  String? sloganFor(bool isArabic) => isArabic ? sloganArabic : slogan;
  String? bioFor(bool isArabic) => isArabic ? bioArabic : bio;
  String? locationFor(bool isArabic) =>
      isArabic ? locationTextArabic : locationText;

  /// The bilingual formatted event date range (e.g. "23-25 نوفمبر 2026"), or
  /// null when the edition's start / end dates are not both set.
  String? eventDateRange(bool isArabic) {
    final start = eventStartDate;
    final end = eventEndDate;
    if (start == null || end == null) {
      return null;
    }
    return formatEventDateRange(start, end, isArabic: isArabic);
  }

  static OrgProfile fromJson(Map<String, dynamic> json) => OrgProfile(
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        currentYear: (json['currentYear'] as num?)?.toInt() ?? 0,
        status: json['status'] as String? ?? '',
        slogan: json['slogan'] as String?,
        sloganArabic: json['sloganArabic'] as String?,
        bio: json['bio'] as String?,
        bioArabic: json['bioArabic'] as String?,
        locationText: json['locationText'] as String?,
        locationTextArabic: json['locationTextArabic'] as String?,
        contactPhone: json['contactPhone'] as String?,
        contactEmail: json['contactEmail'] as String?,
        contactWebsite: json['contactWebsite'] as String?,
        liveStreamUrl: json['liveStreamUrl'] as String?,
        backgroundVideoUrl: json['backgroundVideoUrl'] as String?,
        logoUrl: json['logoUrl'] as String?,
        version: json['version'] as String?,
        eventStartDate: _asDate(json['eventStartDate']),
        eventEndDate: _asDate(json['eventEndDate']),
        social: OrgSocial.fromJson(_asStringMap(json['social'])),
        aboutItems: ((json['aboutItems'] as List?) ?? const <dynamic>[])
            .map((m) => OrgAboutItem.fromJson(_asStringMap(m)))
            .toList(),
        details: ((json['details'] as List?) ?? const <dynamic>[])
            .map((m) => OrgDetail.fromJson(_asStringMap(m)))
            .toList(),
      );
}

/// The fixed set of social-media URLs (null = not set → the control stays
/// inert).
class OrgSocial {
  const OrgSocial({
    this.facebook,
    this.x,
    this.instagram,
    this.linkedin,
    this.youtube,
    this.tiktok,
    this.snapchat,
  });

  final String? facebook;
  final String? x;
  final String? instagram;
  final String? linkedin;
  final String? youtube;
  final String? tiktok;
  final String? snapchat;

  // The API serialises with camelCase (System.Text.Json) → linkedIn/youTube/
  // tikTok. Read those exact keys, not all-lowercase, or CP-set LinkedIn/
  // YouTube/TikTok links silently drop (same fix as SiteSocialLinks).
  static OrgSocial fromJson(Map<String, dynamic> json) => OrgSocial(
        facebook: json['facebook'] as String?,
        x: json['x'] as String?,
        instagram: json['instagram'] as String?,
        linkedin: json['linkedIn'] as String?,
        youtube: json['youTube'] as String?,
        tiktok: json['tikTok'] as String?,
        snapchat: json['snapchat'] as String?,
      );
}

/// One "about" item — a bilingual title + body (e.g. mission / vision).
class OrgAboutItem {
  const OrgAboutItem({
    required this.title,
    required this.titleArabic,
    required this.text,
    required this.textArabic,
  });

  final String title;
  final String titleArabic;
  final String text;
  final String textArabic;

  String titleFor(bool isArabic) => isArabic ? titleArabic : title;
  String textFor(bool isArabic) => isArabic ? textArabic : text;

  static OrgAboutItem fromJson(Map<String, dynamic> json) => OrgAboutItem(
        title: json['title'] as String? ?? '',
        titleArabic: json['titleArabic'] as String? ?? '',
        text: json['text'] as String? ?? '',
        textArabic: json['textArabic'] as String? ?? '',
      );
}

/// One "detail" row — a bilingual label + a value (e.g. year / date /
/// location).
class OrgDetail {
  const OrgDetail({
    required this.name,
    required this.nameArabic,
    required this.value,
    this.valueArabic,
  });

  final String name;
  final String nameArabic;
  final String value;

  /// The Arabic value; null/blank for a language-neutral value (a year, a URL),
  /// in which case [valueFor] falls back to [value].
  final String? valueArabic;

  String nameFor(bool isArabic) => isArabic ? nameArabic : name;

  String valueFor(bool isArabic) {
    final ar = valueArabic;
    return isArabic && ar != null && ar.isNotEmpty ? ar : value;
  }

  static OrgDetail fromJson(Map<String, dynamic> json) => OrgDetail(
        name: json['name'] as String? ?? '',
        nameArabic: json['nameArabic'] as String? ?? '',
        value: json['value'] as String? ?? '',
        valueArabic: json['valueArabic'] as String?,
      );
}

/// App-local data layer for the public Organization profile — reads/writes the
/// on-device cache (`shared_preferences`) and revalidates against the server
/// with
/// the `Last-Modified` / 304 conditional GET.
class OrgProfileRepository {
  OrgProfileRepository(this._client, this._prefs);

  final SimfApiClient _client;
  final SimfPrefsStorage _prefs;

  /// The last profile saved on this device (instant, synchronous). Null on the
  /// first-ever run, before any successful fetch.
  OrgProfile? loadCached() {
    final raw = _prefs.getString(StorageKeys.orgProfileJson);
    if (raw == null || raw.isEmpty) {
      return null;
    }
    try {
      return OrgProfile.fromJson(_asStringMap(jsonDecode(raw)));
    } on Object {
      return null; // corrupt cache → treat as absent
    }
  }

  /// Revalidate against the server using the saved `Last-Modified` token and
  /// persist the result. Returns the fresh profile when it changed; null when
  /// the
  /// server reports `304` (unchanged) so the caller keeps the cached value.
  Future<OrgProfile?> refresh() async {
    final result = await _client.getConditional<Object?>(
      '/app/organization-profile',
      ifModifiedSince: _prefs.getString(StorageKeys.orgProfileLastModified),
      decodeData: (data) => data,
    );
    // Keep the cached value untouched on 304 (unchanged) or a missing /
    // non-object
    // body — never overwrite a good local profile with an empty one.
    if (result.notModified || result.data is! Map) {
      return null;
    }
    final map = _asStringMap(result.data);
    await _prefs.setString(StorageKeys.orgProfileJson, jsonEncode(map));
    final token = result.lastModified;
    if (token != null && token.isNotEmpty) {
      await _prefs.setString(StorageKeys.orgProfileLastModified, token);
    }
    return OrgProfile.fromJson(map);
  }
}

final orgProfileRepositoryProvider = Provider<OrgProfileRepository>((ref) {
  return OrgProfileRepository(
    ref.watch(simfApiClientProvider),
    ref.watch(simfPrefsStorageProvider),
  );
});

/// The Organization profile as an **app-lifetime shared value**: hydrated
/// synchronously from local storage when first read (so every page has it
/// instantly — even offline / on the first frame), then refreshed from the API
/// at
/// the splash via [OrgProfileController.warm] with the `Last-Modified` / 304
/// version-check. Pages read it directly (`ref.watch(orgProfileProvider)`) and
/// fall back to their bundled l10n when it is still null (first run / offline).
class OrgProfileController extends Notifier<OrgProfile?> {
  @override
  OrgProfile? build() => ref.read(orgProfileRepositoryProvider).loadCached();

  /// Refresh from the API + persist locally; keeps the cached value when the
  /// server reports 304 (unchanged) or when offline / on error. Fire-and-forget
  /// from the splash.
  Future<void> warm() async {
    try {
      final fresh = await ref.read(orgProfileRepositoryProvider).refresh();
      if (fresh != null) {
        state = fresh;
      }
    } on Object {
      // Offline / transient error — keep the locally-cached value.
    }
  }
}

final orgProfileProvider =
    NotifierProvider<OrgProfileController, OrgProfile?>(OrgProfileController.new);
