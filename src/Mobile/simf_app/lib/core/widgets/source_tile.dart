import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/core/widgets/simf_field_style.dart';

/// One source row. Named for a screen reader so the sheet has no unlabelled
/// tappable (BUG-019 / 19h).
class SourceTile extends StatelessWidget {
  const SourceTile({
    required this.icon,
    required this.label,
    required this.source,
    super.key,
  });

  final IconData icon;
  final String label;
  final ImageSource source;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      child: ListTile(
        key: ValueKey<String>('imageSource_${source.name}'),
        leading: Icon(icon, color: SimfTokens.accent),
        title: Text(label, style: simfInputStyle),
        onTap: () => Navigator.of(context).pop(source),
      ),
    );
  }
}
