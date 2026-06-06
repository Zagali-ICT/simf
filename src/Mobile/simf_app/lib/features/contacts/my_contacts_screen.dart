import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../core/sharing/content_sharer.dart';
import 'data/contact_models.dart';
import 'data/contacts_repository.dart';
import 'widgets/contact_card.dart';

/// My Contacts (SIMF-FDS-014 §5.6, D-286). **Auth-gated** (Approved only). Lists
/// the cards the visitor saved (`GET /app/contacts`, resolved on read — no PII
/// snapshot). A row opens a detail sheet to **export** the saved card as a vCard
/// (`GET /app/contacts/{id}/vcard`) or **remove** it (`DELETE /app/contacts/{id}`,
/// soft-delete). The app-bar scan action opens the scanner to add more. UI is
/// interim (final visuals from SIMF-VID-001).
class MyContactsScreen extends ConsumerStatefulWidget {
  const MyContactsScreen({super.key});

  @override
  ConsumerState<MyContactsScreen> createState() => _MyContactsScreenState();
}

class _MyContactsScreenState extends ConsumerState<MyContactsScreen> {
  bool _loading = true;
  bool _error = false;
  List<SavedContactRow> _rows = const <SavedContactRow>[];

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final rows = await ref.read(contactsRepositoryProvider).listSaved();
      if (!mounted) {
        return;
      }
      setState(() {
        _rows = rows;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _rows = const <SavedContactRow>[];
        _loading = false;
      });
    }
  }

  Future<void> _openScanner() async {
    await context.pushNamed(RouteNames.scanContact);
    // A save on the scanner closes it and returns here — reload to show it.
    if (mounted) {
      await _load();
    }
  }

  Future<void> _openDetail(SavedContactRow row) async {
    final removed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => _SavedContactSheet(row: row),
    );
    if (removed == true && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).myContactsRemoved)),
      );
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.myContactsTitle),
        actions: <Widget>[
          IconButton(
            tooltip: l10n.contactScanAdd,
            onPressed: () => unawaited(_openScanner()),
            icon: const Icon(Icons.qr_code_scanner),
          ),
        ],
      ),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return _ErrorState(
        message: l10n.myContactsError,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_rows.isEmpty) {
      return _EmptyState(
        title: l10n.myContactsEmpty,
        hint: l10n.myContactsEmptyHint,
        actionLabel: l10n.contactScanAdd,
        onAction: () => unawaited(_openScanner()),
      );
    }
    final isArabic = l10n.isArabic;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(SimfTokens.space4),
        itemCount: _rows.length,
        itemBuilder: (context, index) {
          final row = _rows[index];
          return _SavedRow(
            row: row,
            isArabic: isArabic,
            onTap: () => unawaited(_openDetail(row)),
          );
        },
      ),
    );
  }
}

/// One saved-contact row — name, an org/title subtitle, chevron. Tapping opens
/// the detail sheet.
class _SavedRow extends StatelessWidget {
  const _SavedRow({
    required this.row,
    required this.isArabic,
    required this.onTap,
  });

  final SavedContactRow row;
  final bool isArabic;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final name = row.subjectAvailable
        ? row.localizedName(isArabic)
        : l10n.contactUnavailable;
    final subtitleParts = <String>[
      if (row.jobTitle != null && row.jobTitle!.trim().isNotEmpty) row.jobTitle!,
      if (row.organisation != null && row.organisation!.trim().isNotEmpty)
        row.organisation!,
    ];
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        leading: const Icon(
          Icons.account_circle_outlined,
          color: SimfTokens.accent,
        ),
        title: Text(name),
        subtitle: subtitleParts.isEmpty ? null : Text(subtitleParts.join(' · ')),
        trailing: const Icon(Icons.chevron_right, color: SimfTokens.inkMuted),
        onTap: onTap,
      ),
    );
  }
}

/// The saved-contact detail sheet — the full card plus Export vCard / Remove.
class _SavedContactSheet extends ConsumerStatefulWidget {
  const _SavedContactSheet({required this.row});

  final SavedContactRow row;

  @override
  ConsumerState<_SavedContactSheet> createState() => _SavedContactSheetState();
}

class _SavedContactSheetState extends ConsumerState<_SavedContactSheet> {
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
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.myContactsRemoveConfirmTitle),
        content: Text(l10n.myContactsRemoveConfirmBody),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.cancelLabel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.myContactsRemove),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
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
              jobTitle: row.jobTitle,
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

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.title,
    required this.hint,
    required this.actionLabel,
    required this.onAction,
  });

  final String title;
  final String hint;
  final String actionLabel;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            const Icon(
              Icons.contacts_outlined,
              size: 56,
              color: SimfTokens.inkMuted,
            ),
            const SizedBox(height: SimfTokens.space3),
            Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: SimfTokens.space1),
            Text(
              hint,
              textAlign: TextAlign.center,
              style: const TextStyle(color: SimfTokens.inkMuted),
            ),
            const SizedBox(height: SimfTokens.space4),
            FilledButton.icon(
              onPressed: onAction,
              icon: const Icon(Icons.qr_code_scanner),
              label: Text(actionLabel),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
