import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../sessions/data/session_models.dart';
import 'session_summary_screen.dart' show aiSummarySessionsProvider;

/// #1/#6 — **Session summaries list** (`/session-summaries`, Guest+). The home
/// "ملخص الجلسات" tile opens this list of every programme session; tapping one
/// opens the AI-summary **details** page (#34, `AiSummaryScreen`) for that
/// session. The session-detail "ملخص الجلسة" button opens the same details page.
/// Reuses `GET /app/programme/sessions` — no new API.
class SessionSummaryListScreen extends ConsumerWidget {
  const SessionSummaryListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(aiSummarySessionsProvider);
    return KsaPage(
      title: l10n.aiSummaryTitle,
      onBack: () => ksaBackOrHome(context),
      body: sessions.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => KsaErrorState(
          message: l10n.aiSummaryError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(aiSummarySessionsProvider),
        ),
        data: (items) => items.isEmpty
            ? KsaEmptyState(
                icon: Icons.summarize_outlined,
                message: l10n.aiSummaryNoSessions,
              )
            : _List(items: items, l10n: l10n),
      ),
    );
  }
}

class _List extends StatelessWidget {
  const _List({required this.items, required this.l10n});

  final List<SessionListItem> items;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
        SimfTokens.space4,
        SimfTokens.space5,
      ),
      itemCount: items.length + 1,
      separatorBuilder: (_, __) => const SizedBox(height: SimfTokens.space3),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Padding(
            padding: const EdgeInsets.only(bottom: SimfTokens.space2),
            child: Text(
              l10n.aiSummaryChooseSession,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
              ),
            ),
          );
        }
        final item = items[index - 1];
        return _SummaryRow(
          title: item.localizedTitle(isArabic),
          subtitle: _subtitle(context, item, isArabic),
          onTap: () => context.pushNamed(
            RouteNames.aiSummary,
            queryParameters: <String, String>{'sessionId': item.id},
          ),
        );
      },
    );
  }

  String _subtitle(BuildContext context, SessionListItem item, bool isArabic) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final hall = item.localizedHall(isArabic);
    return hall == null || hall.isEmpty ? time : '$time · $hall';
  }
}

/// One tappable session row → its AI summary details. A navy card with the title
/// over the time · hall line and a direction-aware forward chevron.
class _SummaryRow extends StatelessWidget {
  const _SummaryRow({
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    subtitle,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            Icon(
              Directionality.of(context) == TextDirection.rtl
                  ? Icons.chevron_left
                  : Icons.chevron_right,
              size: 20,
              color: SimfTokens.beigeBorder,
            ),
          ],
        ),
      ),
    );
  }
}
