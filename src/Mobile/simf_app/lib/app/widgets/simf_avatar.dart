import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/avatar_fallback.dart';
import 'package:simf_app/app/widgets/simf_image_viewer.dart';
import 'package:simf_app/features/account/data/profile_repository.dart'
    show myAvatarBytesProvider;

// The signed-in account's avatar.
// Split out of `simf_page_shell.dart` (one widget group per file,
// CLAUDE.md section 1). That file re-exports this one, so every existing
// import of the shell keeps resolving.

/// The gold rounded-square avatar (home header 203:1238 / profile identity
/// cards). When [currentUser] is true it shows the **signed-in user's** photo,
/// fetched as authenticated bytes via [myAvatarBytesProvider] (the avatar
/// endpoint is bearer-gated and behind self-signed TLS, so a bare
/// `Image.network` can't load it) and refreshed immediately after an upload via
/// the avatar bust token. Otherwise — and whenever no photo is available — it
/// renders the brand-mark fallback. [name] drives the accessibility label only.
///
/// Owner 2026-07-26 — set [enableFullScreen] on a DISPLAY-ONLY photo (the badge
/// card) so tapping it opens the picture full size from the already-fetched
/// bytes (D-422: the avatar endpoint is bearer-gated, so the viewer paints a
/// [MemoryImage], never a bare `Image.network`). It stays off where the tap
/// already means something else (My-Area's change-photo affordance).
class SimfAvatar extends ConsumerWidget {
  const SimfAvatar({
    required this.name,
    this.currentUser = false,
    this.size = 42,
    this.enableFullScreen = false,
    super.key,
  });

  final String name;

  /// True for the signed-in user's own avatar (home / badge / my-area). False
  /// for any other person's avatar (e.g. a question submitter) — that always
  /// shows the fallback, never the signed-in user's photo.
  final bool currentUser;
  final double size;

  /// Opens the photo full size on tap. Only honoured when a real photo is
  /// shown — the brand-mark fallback has nothing to enlarge.
  final bool enableFullScreen;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final fallback = AvatarFallback(size: size);
    Widget child = fallback;
    MemoryImage? photo;
    if (currentUser) {
      final bytes = ref.watch(myAvatarBytesProvider).asData?.value;
      if (bytes != null && bytes.isNotEmpty) {
        photo = MemoryImage(bytes);
        child = Image(
          image: photo,
          fit: BoxFit.cover,
          gaplessPlayback: true,
          errorBuilder: (_, __, ___) => fallback,
        );
      }
    }
    final label = name.trim().isEmpty ? null : name;
    final box = ClipRRect(
      borderRadius: const BorderRadius.all(Radius.circular(SimfTokens.radius)),
      child: SizedBox(width: size, height: size, child: child),
    );
    if (!enableFullScreen || photo == null) {
      return Semantics(image: true, label: label, child: box);
    }
    return SimfTapToEnlarge(
      image: photo,
      label: label ?? '',
      child: box,
    );
  }
}
