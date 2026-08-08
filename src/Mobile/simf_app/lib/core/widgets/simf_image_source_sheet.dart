import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_label.dart';
import 'package:simf_app/core/widgets/source_tile.dart';

/// BUG-019 / 19f — the shared "where does this image come from?" chooser.
///
/// Every attachment used to jump straight to the photo GALLERY, which forces a
/// walk-in registration desk to leave the app to shoot the visitor's document.
/// This beige sheet offers both sources with bilingual labels; the caller keeps
/// owning the [ImagePicker] call so nothing about the picked bytes changes.
///
/// Returns the chosen [ImageSource], or null when the sheet is dismissed.
Future<ImageSource?> showSimfImageSourceSheet(BuildContext context) {
  return showModalBottomSheet<ImageSource>(
    context: context,
    backgroundColor: SimfTokens.cardBeige,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(
        top: Radius.circular(SimfTokens.radiusLarge),
      ),
    ),
    builder: (_) => const SimfImageSourceSheet(),
  );
}

/// The sheet body: a caption then the camera / file rows. Pops the picked
/// [ImageSource].
class SimfImageSourceSheet extends StatelessWidget {
  const SimfImageSourceSheet({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SafeArea(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              SimfTokens.space4,
              SimfTokens.space5,
              SimfTokens.space4,
              SimfTokens.space2,
            ),
            child: SimfFieldLabel(
              l10n.attachSourceTitle,
              color: SimfTokens.navy,
              fontSize: SimfTokens.textLg,
            ),
          ),
          SourceTile(
            icon: Icons.photo_camera_outlined,
            label: l10n.attachFromCamera,
            source: ImageSource.camera,
          ),
          SourceTile(
            icon: Icons.folder_open_outlined,
            label: l10n.attachFromFile,
            source: ImageSource.gallery,
          ),
          const SizedBox(height: SimfTokens.space2),
        ],
      ),
    );
  }
}

