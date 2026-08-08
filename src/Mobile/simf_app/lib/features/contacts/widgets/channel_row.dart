import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/tokens.dart';

/// One labelled channel line (icon + value). Renders nothing when the value is
/// blank, so the card only shows the channels the subject actually exposes.
class ChannelRow extends StatelessWidget {
  const ChannelRow({required this.icon, required this.value, super.key});

  final IconData icon;
  final String? value;

  @override
  Widget build(BuildContext context) {
    if (value == null || value!.trim().isEmpty) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.only(top: SimfTokens.space3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(icon, size: SimfTokens.channelRowSize, color: SimfTokens.accent),
          const SizedBox(width: SimfTokens.space2),
          Expanded(child: Text(value!)),
        ],
      ),
    );
  }
}
