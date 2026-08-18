import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/core/net/core_endpoints.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-461 — the public site/app branding settings (`GET /app/site-settings`):
/// the registration welcome message (bilingual) + the social links. Editable in
/// the Control Panel; social fields are null when not configured. Consumers
/// fall back to their own defaults while loading / on error (offline-safe).
class SiteSettings {
  const SiteSettings({
    required this.registrationMessageAr,
    required this.registrationMessageEn,
    required this.social,
    this.partnerDirectoryEnabled = true,
    this.sessionRatingEnabled = true,
  });

  factory SiteSettings.fromJson(Map<String, dynamic> json) => SiteSettings(
        registrationMessageAr:
            json['registrationSuccessMessageAr'] as String? ?? '',
        registrationMessageEn:
            json['registrationSuccessMessageEn'] as String? ?? '',
        social: SiteSocialLinks.fromJson(
          (json['social'] as Map?)?.cast<String, dynamic>() ??
              const <String, dynamic>{},
        ),
        partnerDirectoryEnabled:
            json['partnerDirectoryEnabled'] as bool? ?? true,
        sessionRatingEnabled: json['sessionRatingEnabled'] as bool? ?? true,
      );

  final String registrationMessageAr;
  final String registrationMessageEn;
  final SiteSocialLinks social;

  /// Build #13 — the CP switch for the "Meet People Like You" partner
  /// directory. Defaults to true (fail-open) while loading / on error / on an
  /// older payload.
  final bool partnerDirectoryEnabled;

  /// 2026-07-22 — the CP RatingConfig "Session" rating-type toggle. When false
  /// the app suppresses the after-watch rate prompt. Defaults to true
  /// (fail-open) while loading / on error / on an older payload.
  final bool sessionRatingEnabled;

  /// The welcome message for [languageCode] ('ar' → Arabic, else English).
  String messageFor(String languageCode) =>
      languageCode == 'ar' ? registrationMessageAr : registrationMessageEn;
}

/// The configurable social-media URLs (null = not set → the control stays
/// inert).
class SiteSocialLinks {
  const SiteSocialLinks({
    this.facebook,
    this.x,
    this.instagram,
    this.linkedin,
    this.youtube,
    this.tiktok,
    this.snapchat,
  });

  // The API serialises with camelCase (System.Text.Json), so the multi-word
  // keys arrive as `linkedIn` / `youTube` / `tikTok` — read those exact keys,
  // not all-lowercase, or the CP-set LinkedIn/YouTube/TikTok URLs silently drop.
  factory SiteSocialLinks.fromJson(Map<String, dynamic> json) =>
      SiteSocialLinks(
        facebook: json['facebook'] as String?,
        x: json['x'] as String?,
        instagram: json['instagram'] as String?,
        linkedin: json['linkedIn'] as String?,
        youtube: json['youTube'] as String?,
        tiktok: json['tikTok'] as String?,
        snapchat: json['snapchat'] as String?,
      );

  final String? facebook;
  final String? x;
  final String? instagram;
  final String? linkedin;
  final String? youtube;
  final String? tiktok;
  final String? snapchat;
}

/// App-local data layer for the public site-settings (`GET /app/site-settings`).
class SiteSettingsRepository {
  SiteSettingsRepository(this._client);

  final SimfApiClient _client;

  Future<SiteSettings> get() {
    return _client.get<SiteSettings>(
      CoreEndpoints.siteSettings,
      decodeData: (data) => SiteSettings.fromJson(
        (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{},
      ),
    );
  }
}

final siteSettingsRepositoryProvider = Provider<SiteSettingsRepository>((ref) {
  return SiteSettingsRepository(ref.watch(simfApiClientProvider));
});

/// The public site-settings, fetched once and cached for the app lifetime
/// (branding rarely changes). Consumers read `.value` and fall back to
/// their local defaults while loading / on error.
final siteSettingsProvider = FutureProvider<SiteSettings>((ref) {
  return ref.watch(siteSettingsRepositoryProvider).get();
});
