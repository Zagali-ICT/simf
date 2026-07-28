import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_confirm_dialog.dart';
import '../../../core/sharing/content_sharer.dart';
import '../../contacts/widgets/contact_card.dart';
import '../data/exhibitor_models.dart';
import '../data/exhibitor_repository.dart';

/// FR-EXH-002 — the captured-lead detail sheet: the full card plus **Export
/// vCard** and **Remove**.
///
/// The lead list had neither action while My Contacts has had both since D-286,
/// so a mis-scan was permanent and the card could only be read on screen. This
/// deliberately mirrors `SavedContactSheet` (same layout, same confirm-then-pop
/// contract) so the two card lists behave identically. Pops `true` on a
/// successful removal so the list can reload and toast.
class CapturedVisitorSheet extends ConsumerStatefulWidget {
  const CapturedVisitorSheet({required this.visitor, super.key});

  final ExhibitorVisitor visitor;

  @override
  ConsumerState<CapturedVisitorSheet> createState() =>
      _CapturedVisitorSheetState();
}

class _CapturedVisitorSheetState extends ConsumerState<CapturedVisitorSheet> {
  bool _busy = false;

  Future<void> _exportVcard() async {
    final l10n = AppL10n.of(context);
    // Captured before the await: the share sheet's anchor rect belongs to the
    // tapped widget, so reading it after the fetch is a use-across-async-gap.
    final origin = shareOriginFromContext(context);
    setState(() => _busy = true);
    try {
      final repository = ref.read(exhibitorRepositoryProvider);
      final vcf = await repository.getVcard(widget.visitor.id);
      await shareTextContent(
        content: vcf,
        filename: 'simf-lead.vcf',
        mimeType: 'text/vcard',
        sharePositionOrigin: origin,
      );
      if (mounted) {
        setState(() => _busy = false);
      }
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _busy = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.shareFailed)),
      );
    }
  }

  Future<void> _remove() async {
    final l10n = AppL10n.of(context);
    final confirmed = await SimfConfirmDialog.show(
      context,
      title: l10n.myVisitorsRemoveConfirmTitle,
      message: l10n.myVisitorsRemoveConfirmBody,
      confirmLabel: l10n.myVisitorsRemove,
      isDestructive: true,
    );
    if (!confirmed || !mounted) {
      return;
    }
    setState(() => _busy = true);
    try {
      await ref
          .read(exhibitorRepositoryProvider)
          .removeVisitor(widget.visitor.id);
      if (!mounted) {
        return;
      }
      Navigator.of(context).pop(true);
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() => _busy = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(l10n.scanVisitorError)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final card = widget.visitor.card;
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            ContactCard(
              name: card.localizedName(isArabic),
              available: card.available,
              jobTitle: card.localizedJobTitle(isArabic),
              organisation: card.localizedOrganisation(isArabic),
              country: card.localizedCountry(isArabic),
              email: card.email,
              saudiMobile: card.saudiMobile,
              internationalMobile: card.internationalMobile,
              note: widget.visitor.note,
            ),
            const SizedBox(height: SimfTokens.space3),
            // A gone subject has no card to export — only the removal stays
            // useful, matching the My-Contacts sheet.
            if (card.available)
              OutlinedButton.icon(
                onPressed: _busy ? null : () => unawaited(_exportVcard()),
                icon: const Icon(Icons.ios_share),
                label: Text(l10n.myVisitorsExportVcard),
              ),
            const SizedBox(height: SimfTokens.space2),
            FilledButton.icon(
              onPressed: _busy ? null : () => unawaited(_remove()),
              icon: const Icon(Icons.delete_outline),
              style: FilledButton.styleFrom(backgroundColor: SimfTokens.danger),
              label: Text(l10n.myVisitorsRemove),
            ),
          ],
        ),
      ),
    );
  }
}
