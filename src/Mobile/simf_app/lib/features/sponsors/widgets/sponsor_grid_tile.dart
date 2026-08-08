import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/sponsors/widgets/sponsor_logo.dart';

/// One gold-tier grid tile (frame 925:3031): a single 72-high navy card on the
/// beige hairline holding the sponsor logo above its name (12px SemiBold white,
/// centred). The logo fills the area above the name; initials are the fallback.
class SponsorGridTile extends StatelessWidget {
  const SponsorGridTile({required this.id, required this.baseUrl, required this.name, required this.initials, required this.onTap, super.key,
  });

  final String id;
  final String baseUrl;
  final String name;
  final String initials;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.navyDeep,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        side: const BorderSide(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space2),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Expanded(
                child: SponsorLogo(
                  id: id,
                  baseUrl: baseUrl,
                  fallbackInitials: initials,
                  hero: false,
                  name: name,
                ),
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                name,
                textAlign: TextAlign.center,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: SimfTokens.labelWhiteSemiboldSm,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
