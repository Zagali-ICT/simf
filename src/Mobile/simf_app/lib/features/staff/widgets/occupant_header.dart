import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/staff/data/staff_seating_models.dart';

/// The guest's photo + name. The bytes arrive from the authenticated Dio path
/// (D-422); until they do (or when there is no photo) a labelled placeholder
/// keeps the row's height stable and the a11y tree named.
class OccupantHeader extends StatelessWidget {
  const OccupantHeader({
    required this.result,
    required this.photo,
    required this.l10n,
    super.key,
  });

  final StaffSeatOccupant result;
  final Uint8List? photo;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final bytes = photo;
    return Row(
      children: <Widget>[
        Semantics(
          image: true,
          label: l10n.staffSeatingGuestPhoto,
          child: ClipRRect(
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: SizedBox(
              width: SimfTokens.staffSeatingPhotoSize,
              height: SimfTokens.staffSeatingPhotoSize,
              child: bytes == null
                  ? const ColoredBox(
                      color: SimfTokens.navy,
                      child: Icon(
                        Icons.person_outline,
                        color: SimfTokens.beigeBorder,
                      ),
                    )
                  : Image.memory(bytes, fit: BoxFit.cover),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: Text(
            result.localizedGuestHint(isArabic: l10n.isArabic) ??
                result.localizedName(isArabic: l10n.isArabic),
            style: SimfTokens.labelWhiteBoldTitle,
          ),
        ),
      ],
    );
  }
}
