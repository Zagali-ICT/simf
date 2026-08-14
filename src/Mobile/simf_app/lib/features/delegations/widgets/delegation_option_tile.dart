import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/delegations/data/delegation_models.dart';
import 'package:simf_app/features/speakers/widgets/speaker_option_tile.dart' show SpeakerOptionTile;

/// One selectable delegation row in the picker — flag + localized country name +
/// member count, with a selected (gold) outline. Mirrors [SpeakerOptionTile]'s
/// role for the speaker picker.
class DelegationOptionTile extends StatelessWidget {
  const DelegationOptionTile({required this.delegation, required this.isArabic, required this.selected, required this.onTap, super.key,
  });

  final DelegationItem delegation;
  final bool isArabic;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: SimfTokens.surface,
      borderRadius: SimfTokens.borderRadiusSmall,
      child: InkWell(
        onTap: onTap,
        borderRadius: SimfTokens.borderRadiusSmall,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: SimfTokens.space3,
            vertical: SimfTokens.space3,
          ),
          decoration: BoxDecoration(
            borderRadius: SimfTokens.borderRadiusSmall,
            border: Border.all(
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
              width: selected ? 2 : 1,
            ),
          ),
          child: Row(
            children: <Widget>[
              Text(
                delegation.flagEmoji,
                style: const TextStyle(fontSize: SimfTokens.delegationOptionTileFontSize),
              ),
              const SizedBox(width: SimfTokens.space3),
              Expanded(
                child: Text(
                  delegation.localizedCountry(isArabic),
                  style: SimfTokens.labelNavyMediumSm,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              const SizedBox(width: SimfTokens.space2),
              Text(
                '${delegation.memberCount}',
                style: SimfTokens.bodyGreySm,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
