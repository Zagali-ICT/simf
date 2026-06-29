import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../sessions/data/session_models.dart';
import '../sessions/data/sessions_repository.dart';
import '../sessions/widgets/session_filter_tabs.dart';
import 'data/session_summary_models.dart';
import 'data/session_summary_repository.dart';

/// The programme list, reused for the summaries list + this screen's session
/// resolution + day agenda (`GET /app/programme/sessions`).
final aiSummarySessionsProvider =
    FutureProvider.autoDispose<List<SessionListItem>>(
  (ref) => ref.watch(sessionsRepositoryProvider).getSessions(),
);

/// 24h "HH:mm" for the session sub-line; 12h "hh:mm a" for the agenda rows
/// (locale-data-free, pinned 'en' for Western digits as the frame shows).
final DateFormat _time24 = DateFormat('HH:mm', 'en');
final DateFormat _agendaTime = DateFormat('hh:mm a', 'en');

const List<String> _weekdaysAr = <String>[
  'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد',
];
const List<String> _weekdaysEn = <String>[
  'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday',
];

String _weekday(DateTime d, bool isArabic) =>
    (isArabic ? _weekdaysAr : _weekdaysEn)[d.weekday - 1];

/// The three summary tabs (Figma 1072:14647), in RTL display order
/// (right→left): أبرز النقاط · التوصيات · المتحدثون. أبرز النقاط is the default.
enum _SummaryTab { keyPoints, recommendations, speakers }

/// Page 034 — ملخص الجلسة · Session summary (#34, `/ai-summary?sessionId=`),
/// rebuilt to the KSA-Project Figma frame **1072:13518**.
///
/// **Public** (Guest+, `AllowAnonymous`). Reached with a `sessionId` from the
/// summaries list (#111) or the session-detail "ملخص الجلسة" button; with no id
/// it falls back to the first programme session. Top-to-bottom: the **"الجلسة"
/// info card** (the selected session's gold title + day·time·duration·hall over a
/// **day-agenda timeline** — that day's sessions, reused from the cached
/// programme, no new API), a **3-tab segmented control** (المتحدثون / أبرز النقاط
/// / التوصيات), a **tab-content card** rendering the active section as gold-dot
/// bullets, and a **"توليد ملخص للجلسة"** card whose gold button expands /
/// collapses the published AI summary paragraph.
///
/// Reads the published summary (`GET /app/programme/sessions/{id}/summary`); a
/// 404 = no published summary yet (the tabs + paragraph show the empty note). The
/// summary is **Committee-generated** in the Control Panel (D-237/D-472) — this
/// screen is a read-only consumer, so the gold button reveals the already-published
/// text rather than triggering generation.
class AiSummaryScreen extends ConsumerStatefulWidget {
  const AiSummaryScreen({this.sessionId, super.key});

  final String? sessionId;

  @override
  ConsumerState<AiSummaryScreen> createState() => _AiSummaryScreenState();
}

class _AiSummaryScreenState extends ConsumerState<AiSummaryScreen> {
  String? _selectedId;
  SessionListItem? _selectedSession;
  bool _loading = false;
  bool _error = false;
  SessionSummary? _summary;
  _SummaryTab _tab = _SummaryTab.keyPoints;
  bool _summaryExpanded = true;

  @override
  void initState() {
    super.initState();
    final id = widget.sessionId?.trim();
    if (id != null && id.isNotEmpty) {
      _selectedId = id;
      unawaited(_load());
    }
  }

  Future<void> _load() async {
    final id = _selectedId;
    if (id == null) {
      return;
    }
    setState(() {
      _loading = true;
      _error = false;
    });
    try {
      final summary =
          await ref.read(sessionSummaryRepositoryProvider).getSummary(id);
      if (!mounted) {
        return;
      }
      setState(() {
        _summary = summary;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      // A 404 = no published summary yet → _summary stays null and the tabs +
      // paragraph show the empty note; any other failure → error + retry.
      setState(() {
        _loading = false;
        _error = failure.httpStatus != 404;
      });
    }
  }

  /// Resolve the selected session (the passed id or the first) + its metadata,
  /// firing the summary load once. Runs after the programme list resolves.
  void _ensureSelection(List<SessionListItem> sessions) {
    if (sessions.isEmpty || (_selectedId != null && _selectedSession != null)) {
      return;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      if (_selectedId == null) {
        setState(() {
          _selectedSession = sessions.first;
          _selectedId = sessions.first.id;
        });
        unawaited(_load());
      } else if (_selectedSession == null) {
        final match = sessions.where((s) => s.id == _selectedId);
        setState(() {
          _selectedSession = match.isNotEmpty ? match.first : sessions.first;
        });
      }
    });
  }

  /// That day's sessions (the agenda timeline) — the cached programme filtered to
  /// the selected session's local calendar day, time-ordered.
  List<SessionListItem> _dayAgenda(List<SessionListItem> all) {
    final selected = _selectedSession;
    if (selected == null) {
      return const <SessionListItem>[];
    }
    final day = selected.startLocal;
    final rows = all
        .where(
          (s) =>
              s.id != selected.id &&
              s.startLocal.year == day.year &&
              s.startLocal.month == day.month &&
              s.startLocal.day == day.day,
        )
        .toList()
      ..sort((a, b) => a.startUtc.compareTo(b.startUtc));
    return rows;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(aiSummarySessionsProvider);
    return SimfPageShell(
      title: l10n.aiSummaryTitle,
      onBack: () => backOrHome(context),
      body: sessions.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => _Inline(
          icon: Icons.event_busy_outlined,
          message: l10n.aiSummaryNoSessions,
        ),
        data: (list) {
          if (list.isEmpty) {
            return _Inline(
              icon: Icons.event_busy_outlined,
              message: l10n.aiSummaryNoSessions,
            );
          }
          _ensureSelection(list);
          return _body(l10n, list);
        },
      ),
    );
  }

  Widget _body(AppL10n l10n, List<SessionListItem> list) {
    final isArabic = l10n.isArabic;
    final selected = _selectedSession;
    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        if (selected != null)
          _SessionInfoCard(
            label: l10n.aiSummarySessionLabel,
            session: selected,
            agenda: _dayAgenda(list),
            isArabic: isArabic,
            durationLabel: l10n.sessionDurationMinutes(
              selected.endLocal.difference(selected.startLocal).inMinutes,
            ),
          ),
        const SizedBox(height: SimfTokens.space4),
        SessionFilterTabs(
          labels: <String>[
            l10n.aiSummaryKeyPointsHeading,
            l10n.aiSummaryRecommendationsHeading,
            l10n.aiSummarySpeakersHeading,
          ],
          selectedIndex: _SummaryTab.values.indexOf(_tab),
          onSelected: (i) =>
              setState(() => _tab = _SummaryTab.values[i]),
          equalWidth: true,
        ),
        const SizedBox(height: SimfTokens.space4),
        _TabContentCard(
          heading: _activeLabel(l10n),
          child: _tabBody(l10n, isArabic),
        ),
        const SizedBox(height: SimfTokens.space4),
        _GenerateCard(
          label: l10n.aiSummaryGenerateButton,
          expanded: _summaryExpanded,
          onToggle: () =>
              setState(() => _summaryExpanded = !_summaryExpanded),
          paragraph: _summaryParagraph(l10n, isArabic),
        ),
      ],
    );
  }

  String _activeLabel(AppL10n l10n) => switch (_tab) {
        _SummaryTab.keyPoints => l10n.aiSummaryKeyPointsHeading,
        _SummaryTab.recommendations => l10n.aiSummaryRecommendationsHeading,
        _SummaryTab.speakers => l10n.aiSummarySpeakersHeading,
      };

  /// The active tab's content — bullets, the empty note, a loader, or retry.
  Widget _tabBody(AppL10n l10n, bool isArabic) {
    if (_loading) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: SimfTokens.space4),
        child: Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
      );
    }
    if (_error) {
      return _ErrorInline(
        message: l10n.aiSummaryError,
        onRetry: () => unawaited(_load()),
      );
    }
    final summary = _summary;
    final block = summary == null
        ? ''
        : switch (_tab) {
            _SummaryTab.keyPoints => summary.localizedKeyPoints(isArabic),
            _SummaryTab.recommendations =>
              summary.localizedRecommendations(isArabic),
            _SummaryTab.speakers => summary.localizedSpeakers(isArabic),
          };
    final lines = block
        .split('\n')
        .map((l) => l.trim())
        .where((l) => l.isNotEmpty)
        .toList(growable: false);
    if (lines.isEmpty) {
      return _Body(l10n.aiSummaryNone);
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        for (var i = 0; i < lines.length; i++) ...<Widget>[
          if (i != 0) const SizedBox(height: SimfTokens.space3),
          _Bullet(text: lines[i]),
        ],
      ],
    );
  }

  /// The published full-text paragraph (or the empty note) under the generate
  /// button.
  String _summaryParagraph(AppL10n l10n, bool isArabic) {
    final text = _summary?.localizedFullText(isArabic).trim() ?? '';
    return text.isEmpty ? l10n.aiSummaryNone : text;
  }
}

/// The "الجلسة" info card (frame 1072:14628): the white Medium label over the
/// bordered selected-session box (gold title + day·time·duration·hall sub-line),
/// then that day's agenda timeline.
class _SessionInfoCard extends StatelessWidget {
  const _SessionInfoCard({
    required this.label,
    required this.session,
    required this.agenda,
    required this.isArabic,
    required this.durationLabel,
  });

  final String label;
  final SessionListItem session;
  final List<SessionListItem> agenda;
  final bool isArabic;
  final String durationLabel;

  @override
  Widget build(BuildContext context) {
    final hall = session.localizedHall(isArabic);
    final sub = <String>[
      _weekday(session.startLocal, isArabic),
      _time24.format(session.startLocal),
      durationLabel,
      if (hall != null && hall.trim().isNotEmpty) hall,
    ].join(' · ');
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space2),
          Text(
            label,
            textAlign: TextAlign.end,
            style: const TextStyle(
              color: Colors.white,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          // The bordered selected-session box (frame 1072:14603).
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space2,
              vertical: SimfTokens.space4,
            ),
            decoration: BoxDecoration(
              border: Border.all(
                color: SimfTokens.beigeBorder,
                width: SimfTokens.hairline,
              ),
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  session.localizedTitle(isArabic),
                  textAlign: TextAlign.end,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontSize: SimfTokens.textLg,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2),
                Text(
                  sub,
                  textAlign: TextAlign.end,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm,
                  ),
                ),
              ],
            ),
          ),
          if (agenda.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            for (final item in agenda) _AgendaRow(item: item, isArabic: isArabic),
          ],
        ],
      ),
    );
  }
}

/// One day-agenda row (frame 1072:14612): the agenda title at the inline-start
/// (right under RTL) and the 12h time at the inline-end (left).
class _AgendaRow extends StatelessWidget {
  const _AgendaRow({required this.item, required this.isArabic});

  final SessionListItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space3,
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              item.localizedTitle(isArabic),
              textAlign: TextAlign.start,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space3),
          Text(
            _agendaTime.format(item.startLocal),
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

/// The active-tab content card (frame 1072:14673): a navy (#01132D) box with the
/// gold-bar heading (bar on the right) and the bullets / note below.
class _TabContentCard extends StatelessWidget {
  const _TabContentCard({required this.heading, required this.child});

  final String heading;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(SimfTokens.space4),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: <Widget>[
          _SectionHeading(heading),
          const SizedBox(height: SimfTokens.space4),
          child,
        ],
      ),
    );
  }
}

/// A section heading (frame 1072:14660): the white Bold label with a gold pill
/// bar (4×20) to its inline-end (right under RTL).
class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 4,
          height: 20,
          decoration: BoxDecoration(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Text(
          text,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: SimfTokens.textMd,
          ),
        ),
      ],
    );
  }
}

/// One bullet row (frame 1072:14666): a 6px gold dot on the inline-start (right
/// under RTL) and the beige point text.
class _Bullet extends StatelessWidget {
  const _Bullet({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const Padding(
          padding: EdgeInsets.only(top: 7),
          child: SizedBox(
            width: 6,
            height: 6,
            child: DecoratedBox(
              decoration: BoxDecoration(
                color: SimfTokens.accent,
                shape: BoxShape.circle,
              ),
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Expanded(
          child: Text(
            text,
            textAlign: TextAlign.start,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w400,
              height: 1.5,
            ),
          ),
        ),
      ],
    );
  }
}

/// The generate-summary card (frame 1433:11389): the gold "توليد ملخص للجلسة"
/// button (sparkle + label on the right, the collapse chevron on the left) over
/// the published summary paragraph, shown when expanded.
class _GenerateCard extends StatelessWidget {
  const _GenerateCard({
    required this.label,
    required this.expanded,
    required this.onToggle,
    required this.paragraph,
  });

  final String label;
  final bool expanded;
  final VoidCallback onToggle;
  final String paragraph;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space4,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Material(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
            child: InkWell(
              onTap: onToggle,
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              child: Container(
                height: 48,
                padding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space4,
                  vertical: SimfTokens.space3,
                ),
                child: Row(
                  children: <Widget>[
                    Expanded(
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          const Icon(
                            Icons.auto_awesome,
                            size: 18,
                            color: Colors.white,
                          ),
                          const SizedBox(width: SimfTokens.space2),
                          Flexible(
                            child: Text(
                              label,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: Colors.white,
                                fontSize: SimfTokens.textLg,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    Icon(
                      expanded
                          ? Icons.keyboard_arrow_up
                          : Icons.keyboard_arrow_down,
                      size: 20,
                      color: Colors.white,
                    ),
                  ],
                ),
              ),
            ),
          ),
          if (expanded) ...<Widget>[
            const SizedBox(height: SimfTokens.space4),
            Text(
              paragraph,
              textAlign: TextAlign.start,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textMd,
                fontWeight: FontWeight.w500,
                height: 1.5,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: SimfTokens.beigeBorder,
        height: 1.5,
        fontSize: SimfTokens.textMd,
      ),
    );
  }
}

class _Inline extends StatelessWidget {
  const _Inline({required this.icon, required this.message});

  final IconData icon;
  final String message;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space8),
      child: Column(
        children: <Widget>[
          Icon(icon, size: 56, color: SimfTokens.txtTertiary),
          const SizedBox(height: SimfTokens.space3),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.txtSecondary),
          ),
        ],
      ),
    );
  }
}

class _ErrorInline extends StatelessWidget {
  const _ErrorInline({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: SimfTokens.space4),
      child: Column(
        children: <Widget>[
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: SimfTokens.txtSecondary),
          ),
          const SizedBox(height: SimfTokens.space4),
          FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
        ],
      ),
    );
  }
}
