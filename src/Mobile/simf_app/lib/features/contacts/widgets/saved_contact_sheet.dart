import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../../app/localization/app_l10n.dart';
import '../../../app/theme/tokens.dart';
import '../../../app/widgets/simf_confirm_dialog.dart';
import '../../../core/sharing/content_sharer.dart';
import '../data/contact_models.dart';
import '../data/contacts_repository.dart';
import 'contact_card.dart';

/// The saved-contact detail sheet — the full card plus Export vCard / Remove.
/// Pops `true` on a successful removal so the list can reload + toast.
class SavedContactSheet extends ConsumerStatefulWidget {
  const SavedContactSheet({required this.row, super.key});

  final SavedContactRow row;

  @override
  ConsumerState<SavedContactSheet> createState() => _SavedContactSheetState();
}

class _SavedContactSheetState extends ConsumerState<SavedContactSheet> {
  bool _busy = false;

  Future<void> _exportVcard() async {
    final l10n = AppL10n.of(context);
    setState(() => _busy = true);
    try {
      final vcf =
          await ref.read(contactsRepositoryProvider).getVcard(widget.row.id);
      await shareTextContent(
        content: vcf,
        filename: 'simf-contact.vcf',
        mimeType: 'text/vcard',
        sharePositionOrigin: shareOriginFromContext(context),
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
      title: l10n.myContactsRemoveConfirmTitle,
      message: l10n.myContactsRemoveConfirmBody,
      confirmLabel: l10n.myContactsRemove,
      isDestructive: true,
    );
    if (!confirmed || !mounted) {
      return;
    }
    setState(() => _busy = true);
    try {
      await ref.read(contactsRepositoryProvider).remove(widget.row.id);
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
        SnackBar(content: Text(l10n.myContactsError)),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final row = widget.row;
    return Padding(
      padding: const EdgeInsets.all(SimfTokens.space4),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            ContactCard(
              name: row.localizedName(isArabic),
              available: row.subjectAvailable,
              jobTitle: row.localizedJobTitle(isArabic),
              organisation: row.organisation,
              note: row.note,
            ),
            const SizedBox(height: SimfTokens.space3),
            if (row.subjectAvailable)
              OutlinedButton.icon(
                onPressed: _busy ? null : () => unawaited(_exportVcard()),
                icon: const Icon(Icons.ios_share),
                label: Text(l10n.myContactsExportVcard),
              ),
            const SizedBox(height: SimfTokens.space2),
            FilledButton.icon(
              onPressed: _busy ? null : () => unawaited(_remove()),
              icon: const Icon(Icons.delete_outline),
              style: FilledButton.styleFrom(backgroundColor: SimfTokens.danger),
              label: Text(l10n.myContactsRemove),
            ),
          ],
        ),
      ),
    );
  }
}
