import 'package:flutter/material.dart';

import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/faq/data/faq_models.dart';

/// One accordion card (frame 1388:7569…): the question row with a gold
/// expand/collapse chevron, revealing the answer below a hairline when open.
class FaqTile extends StatefulWidget {
  const FaqTile({required this.entry, required this.isArabic, super.key});

  final FaqEntry entry;
  final bool isArabic;

  @override
  State<FaqTile> createState() => _FaqTileState();
}

class _FaqTileState extends State<FaqTile> {
  bool _expanded = false;

  @override
  Widget build(BuildContext context) {
    final question = widget.entry.localizedQuestion(widget.isArabic);
    final answer = widget.entry.localizedAnswer(widget.isArabic);
    return SimfCard(
      onTap: () => setState(() => _expanded = !_expanded),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8 (Figma 1388:7577)
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    question,
                    textAlign: TextAlign.start,
                    // beige Medium 14 — Figma 1388:7582
                    style: SimfTokens.labelBeigeMedium,
                  ),
                ),
                const SizedBox(width: SimfTokens.space2),
                Icon(
                  _expanded
                      ? Icons.keyboard_arrow_up_rounded
                      : Icons.keyboard_arrow_down_rounded,
                  color: SimfTokens.accent,
                  size: SimfTokens.faqTileSize,
                ),
              ],
            ),
            if (_expanded) ...<Widget>[
              const SizedBox(height: SimfTokens.space2),
              const Divider(
                height: 1,
                thickness: SimfTokens.hairline,
                color: SimfTokens.beigeBorder,
              ),
              const SizedBox(height: SimfTokens.space2),
              Text(
                answer,
                textAlign: TextAlign.start,
                style: SimfTokens.bodyBeige, // beige 14, line-height 1.5
              ),
            ],
          ],
        ),
      ),
    );
  }
}
