import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_svg_icon.dart';
import 'package:simf_app/features/sessions/data/session_favourites.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The المفضلة heart toggle shown on a session card (Figma 1388:8392 /
/// 1388:9067) — a 32px gold square: solid gold + a filled white heart when
/// favourited, a gold-50% square + an outline white heart otherwise (the
/// frame's two states). Watches the shared [sessionFavouritesProvider] so a
/// toggle here updates every card across both screens; reverts + shows a toast
/// if the server rejects it.
class FavouriteHeartButton extends ConsumerWidget {
  const FavouriteHeartButton({
    required this.sessionId,
    this.filledIcon,
    this.outlineIcon,
    this.filledAsset = AppAssets.heartFilled,
    this.outlineAsset = AppAssets.heartOutline,
    super.key,
  });

  final String sessionId;

  /// An optional saved / unsaved glyph override. Defaults to the exact Figma
  /// heart SVGs ([filledAsset] / [outlineAsset]); the saved-sessions screen
  /// (1701:8928) passes the Material bookmark pair, which takes precedence.
  final IconData? filledIcon;
  final IconData? outlineIcon;

  /// The default heart glyphs (Figma 1388:8465 filled / outline). Used unless a
  /// caller passes [filledIcon] / [outlineIcon].
  final String filledAsset;
  final String outlineAsset;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final favourites = ref.watch(sessionFavouritesProvider);
    final isFavourite = favourites.valueOrNull?.contains(sessionId) ?? false;

    return Material(
      color: isFavourite
          ? SimfTokens.accent
          : SimfTokens.accent.withValues(alpha: SimfTokens.opacityHalf),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        onTap: () => _toggle(context, ref),
        child: SizedBox(
          width: SimfTokens.requestIconBox,
          height: SimfTokens.requestIconBox,
          child: _glyph(isFavourite),
        ),
      ),
    );
  }

  /// The IconData override wins when supplied (bookmark); otherwise the exact
  /// Figma heart SVG for the current state.
  Widget _glyph(bool isFavourite) {
    final icon = isFavourite ? filledIcon : outlineIcon;
    if (icon != null) {
      return Icon(
        icon,
        size: SimfTokens.favouriteHeartButtonSize,
        color: SimfTokens.surface,
      );
    }
    return SimfSvgIcon(
      isFavourite ? filledAsset : outlineAsset,
      size: SimfTokens.favouriteHeartButtonSize,
      color: SimfTokens.surface,
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
