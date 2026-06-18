import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../content/data/content_models.dart';
import '../content/data/content_repository.dart';

/// Page 037 — عن الملتقى · About the forum (#37, `/about`, Guest+).
///
/// **Public.** Reuses the CMS content layer (`GET /app/content/{key}`, D-173)
/// with key `about` — renders the localized body as selectable text. A 404
/// (key not seeded yet) shows the "coming soon" empty state.
class AboutScreen extends ConsumerStatefulWidget {
  const AboutScreen({super.key});

  @override
  ConsumerState<AboutScreen> createState() => _AboutScreenState();
}

class _AboutScreenState extends ConsumerState<AboutScreen> {
  bool _loading = true;
  bool _error = false;
  ContentBlock? _block;

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
      final block =
          await ref.read(contentRepositoryProvider).getContentBlock('about');
      if (!mounted) {
        return;
      }
      setState(() {
        _block = block;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        // 404 = the block is not seeded yet → empty (not an error).
        _block = failure.httpStatus == 404 ? null : _block;
        _error = failure.httpStatus != 404;
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      title: l10n.aboutTitle,
      onBack: () => ksaBackOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space6),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                l10n.aboutError,
                textAlign: TextAlign.center,
                style: const TextStyle(color: SimfTokens.txtSecondary),
              ),
              const SizedBox(height: SimfTokens.space4),
              FilledButton(
                onPressed: () => unawaited(_load()),
                child: Text(l10n.retryLabel),
              ),
            ],
          ),
        ),
      );
    }
    final block = _block;
    if (block == null || !block.hasBody) {
      return Center(
        child: Text(
          l10n.aboutEmpty,
          style: const TextStyle(color: SimfTokens.txtSecondary),
        ),
      );
    }
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _AboutHeroCard(body: block.localizedBody(l10n.isArabic)),
      ],
    );
  }
}

/// The "about the forum" hero card (mockup `.content-hero`): a navy-surface
/// card with a code-style accent kicker above the localized CMS body.
class _AboutHeroCard extends StatelessWidget {
  const _AboutHeroCard({required this.body});

  final String body;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const Text(
              'SIMF · 2026',
              style: TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXs,
                letterSpacing: 1.8,
              ),
            ),
            const SizedBox(height: SimfTokens.space2),
            SelectableText(
              body,
              style: const TextStyle(
                color: Colors.white,
                height: 1.8,
                fontSize: SimfTokens.textMd,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
