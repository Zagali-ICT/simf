import 'package:flutter/material.dart';
import 'package:simf_app/app/theme/app_assets.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_forward_chevron.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';

/// One المزيد row (frame node 512:2126): the label at the inline start and a
/// white forward chevron at the inline end, on the tile chrome.
class MyAreaMoreRow extends StatelessWidget {
  const MyAreaMoreRow({required this.label, required this.onTap, super.key});

  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SimfCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space2,
          vertical: SimfTokens.space4,
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontWeight: FontWeight.w500,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
            const SimfForwardChevron(
              AppAssets.icBack,
              size: SimfTokens.myAreaRowsSize,
              color: SimfTokens.surface,
            ),
          ],
        ),
      ),
    );
  }
}
