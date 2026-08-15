/// The uploaded-media asset routes served by the App API (D-357 unified
/// media-asset pipeline).
///
/// Every uploaded image is served anonymously from one route shape,
/// `{base}/app/assets/{kind}/{id}/image`. Before this file that shape was
/// written out at 18 separate call sites across 14 files, with `SpeakerPhoto`
/// alone repeated six times, so a change to the route meant finding all of
/// them.
///
/// The route and the kind names are part of the **shipped wire contract**
/// (D-219): the deployed app decodes against them, so they are copied here
/// exactly and must not be reshaped.
library;

/// The asset categories the App API serves.
///
/// The enum case is the Dart name; [wireName] is the exact path segment the API
/// expects. They are spelled separately on purpose, so a Dart-side rename can
/// never silently change a URL.
enum AssetKind {
  banner('Banner'),
  boothLogo('BoothLogo'),
  companyLogo('CompanyLogo'),
  exhibitorLogo('ExhibitorLogo'),
  mediaPartnerLogo('MediaPartnerLogo'),
  newsImage('NewsImage'),
  programmeDayImage('ProgrammeDayImage'),
  speakerPhoto('SpeakerPhoto'),
  sponsorLogo('SponsorLogo');

  const AssetKind(this.wireName);

  /// The path segment the API expects, e.g. `SpeakerPhoto`.
  final String wireName;
}

/// Builds the URLs for uploaded media assets.
abstract final class AssetUrls {
  /// `{baseUrl}/app/assets/{kind}/{id}/image`.
  ///
  /// [baseUrl] already includes the `/api/v1` segment, so it is not re-appended.
  /// The route returns 404 when nothing has been uploaded, which every caller
  /// treats as "show the fallback" rather than as an error.
  static String image(String baseUrl, AssetKind kind, String id) =>
      '$baseUrl/app/assets/${kind.wireName}/$id/image';
}
