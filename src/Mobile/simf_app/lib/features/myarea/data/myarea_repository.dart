import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/myarea/data/myarea_endpoints.dart';
import 'package:simf_app/features/myarea/data/myarea_models.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// App-local data layer for the My-Area dashboard (Page_014). The dashboard read
/// is the `ApiResult` envelope; the calendar/contact exports are **raw** text
/// bodies (`text/calendar` / `text/vcard`) fetched via [SimfApiClient.getText].
/// All three require an **Approved** account (`RequireApprovedAccount`); a
/// pending caller gets 403 and the screen shows the limited card (Page_014 L-5).
class MyAreaRepository {
  MyAreaRepository(this._client);

  final SimfApiClient _client;

  Future<MyAreaDashboard> getDashboard() {
    return _client.get<MyAreaDashboard>(
      MyAreaEndpoints.dashboard,
      decodeData: (data) => MyAreaDashboard.fromJson(_asMap(data)),
    );
  }

  /// `GET /app/account/contact-card.vcf` → raw vCard 3.0 text (Page_014 E3).
  Future<String> getContactCardVcf() =>
      _client.getText(MyAreaEndpoints.contactCard);

  /// `GET /app/account/calendar.ics` → raw RFC 5545 calendar text (Page_014 E2).
  Future<String> getCalendarIcs() =>
      _client.getText(MyAreaEndpoints.calendar);

  /// `POST /app/account/avatar` (multipart) — uploads a new avatar image for the
  /// signed-in user (jpeg/png/webp, server-gated to ≤ 2 MB). Returns true on
  /// success; the new image is reflected by the next dashboard read.
  Future<bool> uploadAvatar({
    required List<int> bytes,
    required String filename,
  }) {
    return _client.upload<bool>(
      MyAreaEndpoints.avatar,
      bytes: bytes,
      filename: filename,
      contentType: _avatarMime(filename),
      decodeData: (_) => true,
    );
  }

  static String _avatarMime(String filename) {
    final f = filename.toLowerCase();
    if (f.endsWith('.png')) {
      return 'image/png';
    }
    if (f.endsWith('.webp')) {
      return 'image/webp';
    }
    return 'image/jpeg';
  }

  static Map<String, dynamic> _asMap(Object? data) =>
      (data as Map?)?.cast<String, dynamic>() ?? const <String, dynamic>{};
}

final myAreaRepositoryProvider = Provider<MyAreaRepository>((ref) {
  return MyAreaRepository(ref.watch(simfApiClientProvider));
});

/// True when the signed-in account is an audience (visitor) profile type, false
/// for partner/exhibitor ("Other") types — drives the role-based QR-page actions
/// and the Other-only "My Visitors" menu item (D-426). Sourced from the
/// dashboard identity; defaults true (treats unknown/pending as visitor, hiding
/// exhibitor-only surfaces).
final isVisitorProvider = FutureProvider.autoDispose<bool>((ref) async {
  try {
    final dashboard = await ref.watch(myAreaRepositoryProvider).getDashboard();
    return dashboard.identity.isVisitor;
  } on ApiFailure {
    return true;
  }
});
