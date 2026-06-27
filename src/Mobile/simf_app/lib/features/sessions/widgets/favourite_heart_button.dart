import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/session_favourites.dart';

/// The المفضلة heart toggle shown on a session card (Figma 1388:8392 / 1388:9067)
/// — a 32px gold square: solid gold + a filled white heart when favourited, a
/// gold-50% square + an outline white heart otherwise (the frame's two states).
/// Watches the shared [sessionFavouritesProvider] so a toggle here updates every
/// card across both screens; reverts + shows a toast if the server rejects it.
class FavouriteHeartButton extends ConsumerWidget {
  const FavouriteHeartButton({required this.sessionId, super.key});

  final String sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final favourites = ref.watch(sessionFavouritesProvider);
    final isFavourite = favourites.valueOrNull?.contains(sessionId) ?? false;

    return Material(
      color: isFavourite
          ? SimfTokens.accent
          : SimfTokens.accent.withValues(alpha: 0.5),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: () => _toggle(context, ref),
        child: SizedBox(
          width: 32,
          height: 32,
          child: Icon(
            isFavourite ? Icons.favorite : Icons.favorite_border,
            size: 16,
            color: Colors.white,
          ),
        ),
      ),
    );
  }

  Future<void> _toggle(BuildContext context, WidgetRef ref) async {
    final messenger = ScaffoldMessenger.of(context);
    final l10n = AppL10n.of(context);
    try {
      await ref.read(sessionFavouritesProvider.notifier).toggle(sessionId);
    } on ApiFailure {
      messenger.showSnackBar(
        SnackBar(content: Text(l10n.favouriteToggleError)),
      );
    }
  }
}
