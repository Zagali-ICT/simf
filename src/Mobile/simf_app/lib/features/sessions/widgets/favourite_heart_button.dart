import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../data/session_favourites.dart';

/// The المفضلة heart toggle shown on a session card (Figma 1388:8392 / 1388:9067)
/// — a gold square with a filled heart when favourited, a navy square with an
/// outline heart otherwise. Watches the shared [sessionFavouritesProvider] so a
/// toggle here updates every card across both screens; reverts + shows a toast
/// if the server rejects the change.
class FavouriteHeartButton extends ConsumerWidget {
  const FavouriteHeartButton({required this.sessionId, super.key});

  final String sessionId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final favourites = ref.watch(sessionFavouritesProvider);
    final isFavourite = favourites.valueOrNull?.contains(sessionId) ?? false;

    return Material(
      color: isFavourite ? SimfTokens.accent : SimfTokens.navyDeep,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: () => _toggle(context, ref),
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Icon(
            isFavourite ? Icons.favorite : Icons.favorite_border,
            size: 18,
            color: isFavourite ? Colors.white : SimfTokens.beigeBorder,
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
