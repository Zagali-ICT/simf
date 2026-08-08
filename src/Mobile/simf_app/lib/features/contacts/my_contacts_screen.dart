import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import 'data/contact_models.dart';
import 'data/contacts_repository.dart';
import 'widgets/contacts_empty_state.dart';
import 'widgets/saved_contact_sheet.dart';
import 'widgets/saved_contact_tile.dart';
import 'widgets/error_state.dart';

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
      builder: (sheetContext) => SavedContactSheet(row: row),
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
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: ErrorState(
          message: l10n.myContactsError,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    if (_rows.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: ContactsEmptyState(
          title: l10n.myContactsEmpty,
          hint: l10n.myContactsEmptyHint,
          actionLabel: l10n.contactScanAdd,
          onAction: () => unawaited(_openScanner()),
        ),
      );
    }
    final isArabic = l10n.isArabic;
    return SimfPullToRefresh(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(SimfTokens.space4),
        itemCount: _rows.length,
        itemBuilder: (context, index) {
          final row = _rows[index];
          return SavedContactTile(
            row: row,
            isArabic: isArabic,
            onTap: () => unawaited(_openDetail(row)),
          );
        },
      ),
    );
  }
}

