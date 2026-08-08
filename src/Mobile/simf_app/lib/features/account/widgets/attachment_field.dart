import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../../../app/theme/tokens.dart';
import '../../../core/widgets/simf_field_label.dart';
import 'attach_box.dart';

/// A file/photo attachment field (Figma 168:2977): a label, an optional hint,
/// then either the empty 56px attach box or — once [bytes] is set — a preview
/// row (thumbnail + name + action). Shared by the ID-document and face-photo
/// fields; [round] makes the thumbnail a circle (face) vs a rounded square (ID).
/// The screen owns the state + the pick/remove logic ([onAttach]/[onAction]).
class AttachmentField extends StatelessWidget {
  const AttachmentField({
    required this.label,
    required this.bytes,
    required this.round,
    required this.attachLabel,
    required this.attachIcon,
    required this.onAttach,
    required this.attachedName,
    required this.actionLabel,
    required this.onAction,
    this.hintText,
    this.hintDanger = false,
    super.key,
  });

  final String label;
  final String? hintText;
  final bool hintDanger;
  final Uint8List? bytes;
  final bool round;
  final String attachLabel;
  final IconData attachIcon;
  final VoidCallback onAttach;
  final String attachedName;
  final String actionLabel;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    final data = bytes;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        SimfFieldLabel(label),
        const SizedBox(height: SimfTokens.space2),
        if (hintText != null) ...<Widget>[
          Text(
            hintText!,
            style: TextStyle(
              color: hintDanger ? SimfTokens.danger : SimfTokens.greyText,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
        ],
        if (data == null)
          AttachBox(label: attachLabel, icon: attachIcon, onTap: onAttach)
        else
          Container(
            padding: const EdgeInsets.all(SimfTokens.space2),
            decoration: BoxDecoration(
              border: Border.all(color: SimfTokens.beigeBorder),
              borderRadius: SimfTokens.borderRadiusSmall,
            ),
            child: Row(
              children: <Widget>[
                if (round)
                  ClipOval(
                    child: Image.memory(
                      data,
                      width: 40,
                      height: 40,
                      fit: BoxFit.cover,
                    ),
                  )
                else
                  ClipRRect(
                    borderRadius: SimfTokens.borderRadiusSmall,
                    child: Image.memory(
                      data,
                      width: 40,
                      height: 40,
                      fit: BoxFit.cover,
                    ),
                  ),
                const SizedBox(width: SimfTokens.space3),
                Expanded(
                  child: Text(
                    attachedName,
                    overflow: TextOverflow.ellipsis,
                    style: SimfTokens.bodyInputMd,
                  ),
                ),
                TextButton(
                  onPressed: onAction,
                  style: TextButton.styleFrom(
                    foregroundColor: SimfTokens.accent,
                  ),
                  child: Text(actionLabel),
                ),
              ],
            ),
          ),
      ],
    );
  }
}

