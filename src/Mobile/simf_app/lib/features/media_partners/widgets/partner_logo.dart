import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_logo_image.dart';
import 'package:simf_app/core/utils/initials.dart';
import 'package:simf_app/features/media_partners/widgets/initials_tile.dart';

class PartnerLogo extends StatelessWidget {
  const PartnerLogo({required this.url, required this.name, super.key});

  final String url;
  final String name;

  static const double _size = 48;

  String get _initials => initialsFromWords(name);

  @override
  Widget build(BuildContext context) {
    return ClipRRect(
      borderRadius:
          const BorderRadius.all(Radius.circular(SimfTokens.radiusSmall)),
      child: SizedBox(
        width: _size,
        height: _size,
        child: SimfLogoImage(
          url: url,
          semanticLabel: name,
          placeholder: const ColoredBox(
            color: SimfTokens.navyDeep,
            child: Center(
              child: SizedBox(
                width: SimfTokens.partnerCardWidth,
                height: SimfTokens.partnerCardHeight,
                child: CircularProgressIndicator(
                    strokeWidth: SimfTokens.partnerCardStrokeWidth,),
              ),
            ),
          ),
          // Initials are computed only when the fetch fails — the common
          // success path skips the split.
          onError: () => InitialsTile(initials: _initials),
          enableFullScreen: false,
        ),
      ),
    );
  }
}
