import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/features/contacts/widgets/contact_card.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_models.dart';
import 'package:simf_app/features/exhibitor/data/exhibitor_repository.dart';
import 'package:simf_app/features/exhibitor/widgets/captured_visitor_sheet.dart';
import 'package:simf_app/features/exhibitor/widgets/exhibitor_centered.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// D-426 — زوار جناحي / My Booth Visitors. The exhibitor's ("Other" profile
/// type) captured visitors: everyone they scanned at their booth, newest first,
/// each with the visitor's full card resolved live. Reached from the side
/// drawer (Other-only), the exhibitor home's tools row, and after a successful
/// scan. Approved + non-visitor only (a visitor-tier caller gets 403 → the
/// limited/forbidden surface).
///
/// BUG-025 — this is NOT "My Contacts" (`/contacts`, visitor-to-visitor card
/// sharing). The two lists stay separate pending an owner ruling, so the title
/// names the booth and a [SimfPageNote] states the difference in both
/// languages.
///
/// Route: `RouteNames.myVisitors`.
/// Data: [exhibitorRepositoryProvider].
/// Perf: lazy — builds children on demand (ListView.separated).
class MyVisitorsScreen extends ConsumerStatefulWidget {
  const MyVisitorsScreen({super.key});

  @override
  ConsumerState<MyVisitorsScreen> createState() => _MyVisitorsScreenState();
}

class _MyVisitorsScreenState extends ConsumerState<MyVisitorsScreen> {
  bool _loading = true;
  bool _error = false;
  bool _forbidden = false;
  List<ExhibitorVisitor> _visitors = const <ExhibitorVisitor>[];

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _forbidden = false;
    });
    try {
      final visitors =
          await ref.read(exhibitorRepositoryProvider).listMyVisitors();
      if (!mounted) {
        return;
      }
      setState(() {
        _visitors = visitors;
        _loading = false;
      });
    } on ApiFailure catch (e) {
      if (!mounted) {
        return;
      }
      setState(() {
        _forbidden = e.httpStatus == 403;
        _error = e.httpStatus != 403;
        _loading = false;
      });
    }
  }

  /// FR-EXH-002 — opens the lead's detail sheet (export vCard / remove). A
  /// confirmed removal pops `true`, so the list reloads and toasts.
  Future<void> _openDetail(ExhibitorVisitor visitor) async {
    final removed = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => CapturedVisitorSheet(visitor: visitor),
    );
    if (removed == true && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(AppL10n.of(context).myVisitorsRemoved)),
      );
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return SimfPageShell(
      title: l10n.myVisitorsTitle,
      onBack: () => backOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    // The 403 branch especially: an exhibitor whose booth link lands after the
    // first load would otherwise be stuck on it with no way to re-check.
    if (_forbidden) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: ExhibitorCentered(text: l10n.scanVisitorForbidden),
      );
    }
    if (_error) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: SimfErrorState(
          message: l10n.scanVisitorError,
          retryLabel: l10n.retryLabel,
          onRetry: () => unawaited(_load()),
        ),
      );
    }
    if (_visitors.isEmpty) {
      return SimfRefreshableMessage(
        onRefresh: _load,
        child: ExhibitorCentered(text: l10n.myVisitorsEmpty),
      );
    }
    final isArabic = l10n.isArabic;
    return SimfPullToRefresh(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.all(SimfTokens.space4),
        // +1 leading row: the BUG-025 "these are booth scans, not My Contacts"
        // note, scrolled with the list so it never steals viewport height.
        itemCount: _visitors.length + 1,
        separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
        itemBuilder: (context, index) {
          if (index == 0) {
            return SimfPageNote(text: l10n.myVisitorsNote);
          }
          final v = _visitors[index - 1];
          final card = v.card;
          // FR-EXH-002 — a row now opens the detail sheet (export vCard /
          // remove), the same affordance My Contacts has had since D-286.
          return InkWell(
            onTap: () => unawaited(_openDetail(v)),
            child: ContactCard(
              name: card.localizedName(isArabic: isArabic),
              available: card.available,
              jobTitle: card.localizedJobTitle(isArabic: isArabic),
              organisation: card.localizedOrganisation(isArabic: isArabic),
              country: card.localizedCountry(isArabic: isArabic),
              email: card.email,
              saudiMobile: card.saudiMobile,
              internationalMobile: card.internationalMobile,
            ),
          );
        },
      ),
    );
  }
}
