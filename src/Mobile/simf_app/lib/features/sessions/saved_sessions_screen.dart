import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart' show DateFormat;

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/simf_page_shell.dart';
import '../ai_summary/session_summary_screen.dart' show aiSummarySessionsProvider;
import 'data/session_favourites.dart';
import 'data/session_models.dart';
import 'widgets/favourite_heart_button.dart';
import 'widgets/session_filter_tabs.dart';

/// **Saved sessions** — App "الجلسات المحفوظة" (Figma 1701:8928, Approved
/// account, route `/saved-sessions`), reached from the My-Area saved-sessions
/// counter (D-584). The caller's **favourited** sessions (المفضلة = محفوظة) with
/// a gold "★ N جلسة محفوظة" count header and category chips (الكل + each distinct
/// session category) over the cached programme. Each card taps through to the
/// session detail and carries the bookmark toggle (un-saving drops it from the
/// list). Reuses the cached programme (`aiSummarySessionsProvider`) intersected
/// with `sessionFavouritesProvider` — no new endpoint.
class SavedSessionsScreen extends ConsumerStatefulWidget {
  const SavedSessionsScreen({super.key});

  @override
  ConsumerState<SavedSessionsScreen> createState() =>
      _SavedSessionsScreenState();
}

/// One 12-hour formatter for the card timestamps (hoisted off the build path).
final DateFormat _savedTimeFormat = DateFormat('hh:mm a');

/// Gregorian month names, indexed by month-1, for the card's "12 يناير 2026"
/// date line (Figma 1701:8928) — rendered without an intl locale so no
/// `initializeDateFormatting` is required.
const List<String> _monthsAr = <String>[
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
];
const List<String> _monthsEn = <String>[
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

class _SavedSessionsScreenState extends ConsumerState<SavedSessionsScreen> {
  /// The selected category chip: 0 = الكل (all), i>0 = the (i-1)th category.
  int _categoryIndex = 0;

  /// Pull-to-refresh — re-fetch the programme + the favourites set.
  Future<void> _refresh() async {
    ref
      ..invalidate(aiSummarySessionsProvider)
      ..invalidate(sessionFavouritesProvider);
    await ref.read(aiSummarySessionsProvider.future);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final programme = ref.watch(aiSummarySessionsProvider);

    return SimfPageShell(
      title: l10n.savedSessionsTitle,
      onBack: () => backOrHome(context),
      body: programme.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => SimfPullToRefresh(
          onRefresh: _refresh,
          child: SimfPullableHost(
            child: SimfErrorState(
              message: l10n.aiSummaryError,
              retryLabel: l10n.retryLabel,
              onRetry: () => ref.invalidate(aiSummarySessionsProvider),
            ),
          ),
        ),
        data: (items) => _buildBody(context, l10n, items),
      ),
    );
  }

  Widget _buildBody(
    BuildContext context,
    AppL10n l10n,
    List<SessionListItem> programme,
  ) {
    // Rebuild the whole body (count + chips + list) when the favourites set
    // resolves or a bookmark is toggled here.
    final favourites =
        ref.watch(sessionFavouritesProvider).valueOrNull ?? const <String>{};
    final isArabic = l10n.isArabic;

    final saved = programme
        .where((s) => favourites.contains(s.id))
        .toList(growable: false)
      ..sort((a, b) => a.startUtc.compareTo(b.startUtc));

    // Distinct categories present in the saved set, in first-seen order.
    final categories = <String>[];
    for (final s in saved) {
      final c = s.localizedCategory(isArabic);
      if (c != null && c.isNotEmpty && !categories.contains(c)) {
        categories.add(c);
      }
    }
    final index = _categoryIndex.clamp(0, categories.length);
    final selectedCategory = index == 0 ? null : categories[index - 1];
    final filtered = selectedCategory == null
        ? saved
        : saved
            .where((s) => s.localizedCategory(isArabic) == selectedCategory)
            .toList(growable: false);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: <Widget>[
        const SizedBox(height: SimfTokens.space3),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space4),
          child: _SavedCountRow(
            count: saved.length,
            label: l10n.savedSessionsCountLabel,
          ),
        ),
        const SizedBox(height: SimfTokens.space3),
        if (categories.isNotEmpty) ...<Widget>[
          SessionFilterTabs(
            labels: <String>[l10n.sessionTypeAll, ...categories],
            selectedIndex: index,
            onSelected: (i) => setState(() => _categoryIndex = i),
          ),
          const SizedBox(height: SimfTokens.space3),
        ],
        Expanded(
          child: filtered.isEmpty
              ? SimfPullToRefresh(
                  onRefresh: _refresh,
                  child: SimfPullableHost(
                    child: SimfEmptyState(
                      icon: Icons.bookmark_border,
                      message: l10n.savedSessionsEmpty,
                    ),
                  ),
                )
              : SimfPullToRefresh(
                  onRefresh: _refresh,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(
                      SimfTokens.space4,
                      0,
                      SimfTokens.space4,
                      SimfTokens.space5,
                    ),
                    itemCount: filtered.length,
                    itemBuilder: (context, i) => Padding(
                      padding: const EdgeInsets.only(bottom: SimfTokens.space3),
                      child: _SavedCard(item: filtered[i], isArabic: isArabic),
                    ),
                  ),
                ),
        ),
      ],
    );
  }
}

/// The gold "★ N جلسة محفوظة" count row (Figma 1701:8928): a gold-hairline box
/// with the star + unit label at the inline start (right, RTL) and the count at
/// the end.
class _SavedCountRow extends StatelessWidget {
  const _SavedCountRow({required this.count, required this.label});

  final int count;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space3,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        children: <Widget>[
          const Icon(Icons.star, color: SimfTokens.accent, size: 18),
          const SizedBox(width: SimfTokens.space2),
          Text(
            label,
            style: const TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textMd,
              fontWeight: FontWeight.w500,
            ),
          ),
          const Spacer(),
          Text(
            '$count',
            style: const TextStyle(
              color: SimfTokens.accent,
              fontSize: SimfTokens.textLg,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

/// One saved-session card (Figma 1701:8928): the title over a "{category} ·
/// {hall}" line and a "{date} · {time}" line, with the bookmark toggle on the
/// trailing edge. Tapping the card opens the session detail (17).
class _SavedCard extends StatelessWidget {
  const _SavedCard({required this.item, required this.isArabic});

  final SessionListItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final start = item.startLocal;
    final months = isArabic ? _monthsAr : _monthsEn;
    final date = '${start.day} ${months[start.month - 1]} ${start.year}';
    final time = _savedTimeFormat.format(start);
    final category = item.localizedCategory(isArabic);
    final hall = item.localizedHall(isArabic);
    final place = <String>[
      if (category != null && category.isNotEmpty) category,
      if (hall != null && hall.isNotEmpty) hall,
    ].join(' · ');

    return SimfCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{'sessionId': item.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2), // p-8
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Text(
                    item.localizedTitle(isArabic),
                    textAlign: TextAlign.start,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w500,
                      fontSize: SimfTokens.textMd,
                    ),
                  ),
                  if (place.isNotEmpty) ...<Widget>[
                    const SizedBox(height: SimfTokens.space2),
                    _MetaText(place),
                  ],
                  const SizedBox(height: SimfTokens.space2),
                  _MetaText('$date · $time'),
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            // "Saved" = favourited; the bookmark toggles the same set as the
            // heart on the other screens, so un-saving here drops the card.
            FavouriteHeartButton(
              sessionId: item.id,
              filledIcon: Icons.bookmark,
              outlineIcon: Icons.bookmark_border,
            ),
          ],
        ),
      ),
    );
  }
}

/// A single beige 12px meta line (category·hall / date·time) on the card.
class _MetaText extends StatelessWidget {
  const _MetaText(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: SimfTokens.beigeBorder,
        fontSize: SimfTokens.textSm,
      ),
    );
  }
}
