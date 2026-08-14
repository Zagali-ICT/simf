import 'package:url_launcher/url_launcher.dart';

/// Best-effort external launch (the D-369 contract): a missing handler or a
/// malformed URI must never crash the calling page — the user simply stays
/// where they are. Shared by the home social/discover links and the
/// registration-success contact tiles.
Future<void> launchExternalUri(
  Uri uri, {
  LaunchMode mode = LaunchMode.platformDefault,
}) async {
  try {
    await launchUrl(uri, mode: mode);
  } on Object catch (_) {
    // No handler / malformed URI — keep the user on the page.
  }
}
