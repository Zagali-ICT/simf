import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/features/contacts/data/contact_models.dart';
import 'package:simf_app/features/contacts/data/contacts_repository.dart';
import 'package:simf_app/features/myarea/data/myarea_repository.dart';

/// The visitor's saved contact cards (`GET /app/contacts`).
///
/// No folding needed here, unlike the terms and news providers: "empty" is an
/// empty LIST, which `when`'s data branch already carries, so there is no extra
/// server outcome to map onto null.
final savedContactsProvider =
    FutureProvider.autoDispose<List<SavedContactRow>>(
  (ref) => ref.watch(contactsRepositoryProvider).listSaved(),
);

/// The share token plus the vCard body the QR encodes.
@immutable
class ShareCard {
  const ShareCard({required this.token, required this.vcard});

  final String token;
  final String vcard;

  ShareCard withToken(String next) => ShareCard(token: next, vcard: vcard);
}

/// An `AsyncNotifier`, because ROTATE replaces the token in place.
///
/// D-470 + D-737 — the QR encodes the user's vCard with the share token
/// embedded, and the payload is assembled from the two at render. So a rotate
/// only needs the new token: re-fetching the vCard body, which did not change,
/// would be a wasted round trip. That is what the old `setState(() => _token =
/// token)` did, and [rotate] is where it lives now.
class ShareCardNotifier extends AutoDisposeAsyncNotifier<ShareCard> {
  @override
  Future<ShareCard> build() async {
    final token = await ref.watch(contactsRepositoryProvider).getMyShareToken();
    final vcard = await ref.watch(myAreaRepositoryProvider).getContactCardVcf();
    return ShareCard(token: token, vcard: vcard);
  }

  Future<void> rotate() async {
    final token = await ref.read(contactsRepositoryProvider).rotateShareToken();
    final current = state.valueOrNull;
    if (current != null) {
      state = AsyncValue<ShareCard>.data(current.withToken(token));
    }
  }
}

final shareCardProvider =
    AsyncNotifierProvider.autoDispose<ShareCardNotifier, ShareCard>(
  ShareCardNotifier.new,
);
