import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../contacts/widgets/contact_card.dart';
import 'data/exhibitor_models.dart';
import 'data/exhibitor_repository.dart';

/// D-426 — زواري / My Visitors. The exhibitor's ("Other" profile type) captured
/// visitors: everyone they scanned at their booth, newest first, each with the
/// visitor's full card resolved live. Reached from the side drawer (Other-only)
/// and after a successful scan. Approved + non-visitor only (a visitor-tier
/// caller gets 403 → the limited/forbidden surface).
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
    if (_forbidden) {
      return _Centered(text: l10n.scanVisitorForbidden);
    }
    if (_error) {
      return SimfErrorState(
        message: l10n.scanVisitorError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    if (_visitors.isEmpty) {
      return _Centered(text: l10n.myVisitorsEmpty);
    }
    final isArabic = l10n.isArabic;
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.separated(
        padding: const EdgeInsets.all(SimfTokens.space4),
        itemCount: _visitors.length,
        separatorBuilder: (_, __) =>
            const SizedBox(height: SimfTokens.space3),
        itemBuilder: (context, i) {
          final v = _visitors[i];
          final card = v.card;
          return ContactCard(
            name: card.localizedName(isArabic),
            available: card.available,
            jobTitle: card.jobTitle,
            organisation: card.localizedOrganisation(isArabic),
            country: card.localizedCountry(isArabic),
            email: card.email,
            saudiMobile: card.saudiMobile,
            internationalMobile: card.internationalMobile,
          );
        },
      ),
    );
  }
}

class _Centered extends StatelessWidget {
  const _Centered({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Text(
          text,
          textAlign: TextAlign.center,
          style: const TextStyle(color: SimfTokens.beigeBorder),
        ),
      ),
    );
  }
}
