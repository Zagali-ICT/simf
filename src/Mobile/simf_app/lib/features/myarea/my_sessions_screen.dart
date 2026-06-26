import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../sessions/widgets/favourite_heart_button.dart';
import '../sessions/widgets/session_filter_tabs.dart';
import 'data/my_sessions_models.dart';
import 'data/my_sessions_repository.dart';

/// **My sessions** — App "تفاصيل الجلسات" (Figma 1388:9067, Approved account),
/// reached from the My-Area "my sessions" counter. The caller's booked / joined
/// sessions, partitioned into four tabs computed client-side from the device
/// clock: القادمة (still to come), حضرتها (attended), فاتتني (ended & not
/// attended), and الأرشيف (recorded / published). Each card carries the المفضلة
/// heart and taps through to the session detail. Reads `GET /app/account/sessions`.
class MySessionsScreen extends ConsumerStatefulWidget {
  const MySessionsScreen({super.key});

  @override
  ConsumerState<MySessionsScreen> createState() => _MySessionsScreenState();
}

enum _MySessionsTab { upcoming, attended, missed, archive }

class _MySessionsScreenState extends ConsumerState<MySessionsScreen> {
  _MySessionsTab _tab = _MySessionsTab.upcoming;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final sessions = ref.watch(mySessionsProvider);

    final tabLabels = <String>[
      l10n.mySessionsTabUpcoming,
      l10n.mySessionsTabAttended,
      l10n.mySessionsTabMissed,
      l10n.mySessionsTabArchive,
    ];

    return KsaPage(
      title: l10n.mySessionsTitle,
      onBack: () => ksaBackOrHome(context),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          const SizedBox(height: SimfTokens.space2),
          SessionFilterTabs(
            labels: tabLabels,
            selectedIndex: _MySessionsTab.values.indexOf(_tab),
            onSelected: (i) =>
                setState(() => _tab = _MySessionsTab.values[i]),
          ),
          const SizedBox(height: SimfTokens.space3),
          Expanded(
            child: sessions.when(
              loading: () => const Center(
                child: CircularProgressIndicator(color: SimfTokens.accent),
              ),
              error: (_, __) => KsaErrorState(
                message: l10n.mySessionsError,
                retryLabel: l10n.retryLabel,
                onRetry: () => ref.invalidate(mySessionsProvider),
              ),
              data: (page) => _TabbedList(
                items: _filter(page.items),
                tabLabel: tabLabels[_MySessionsTab.values.indexOf(_tab)],
                l10n: l10n,
              ),
            ),
          ),
        ],
      ),
    );
  }

  List<MyAreaSessionItem> _filter(List<MyAreaSessionItem> items) {
    final nowUtc = DateTime.now().toUtc();
    return items.where((item) {
      switch (_tab) {
        case _MySessionsTab.upcoming:
          return item.isUpcoming(nowUtc);
        case _MySessionsTab.attended:
          return item.attended;
        case _MySessionsTab.missed:
          return item.hasEnded(nowUtc) && !item.attended;
        case _MySessionsTab.archive:
          return item.isArchived;
      }
    }).toList(growable: false);
  }
}

class _TabbedList extends StatelessWidget {
  const _TabbedList({
    required this.items,
    required this.tabLabel,
    required this.l10n,
  });

  final List<MyAreaSessionItem> items;
  final String tabLabel;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    if (items.isEmpty) {
      return KsaEmptyState(
        icon: Icons.event_note_outlined,
        message: l10n.mySessionsEmpty,
      );
    }
    final isArabic = l10n.isArabic;
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        0,
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
              l10n.mySessionsCount(items.length, tabLabel),
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textLg,
                fontWeight: FontWeight.w600,
              ),
            ),
          );
        }
        return _MySessionCard(item: items[index - 1], isArabic: isArabic);
      },
    );
  }
}

/// One my-session card: the heart on the trailing edge, the title over a
/// clock·time line with the category chip, and the primary speaker + hall.
class _MySessionCard extends StatelessWidget {
  const _MySessionCard({required this.item, required this.isArabic});

  final MyAreaSessionItem item;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final time = TimeOfDay.fromDateTime(item.startLocal).format(context);
    final category = item.localizedCategory(isArabic);
    final speaker = item.localizedSpeaker(isArabic);
    final hall = item.localizedHall(isArabic);

    return KsaCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{'sessionId': item.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    item.localizedTitle(isArabic),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textLg,
                    ),
                  ),
                  const SizedBox(height: SimfTokens.space2),
                  _MetaLine(
                    icon: Icons.access_time,
                    text: '$time · ${l10nDuration(context)}',
                    trailingChip: category,
                  ),
                  if (speaker != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    _MetaLine(
                      icon: Icons.person_outline,
                      text: _speakerText(speaker),
                      trailingChip: hall,
                      chipBordered: false,
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space3),
            FavouriteHeartButton(sessionId: item.id),
          ],
        ),
      ),
    );
  }

  String l10nDuration(BuildContext context) =>
      AppL10n.of(context).sessionDurationMinutes(item.durationMinutes);

  String _speakerText(String speaker) {
    final title = item.speakerTitle?.trim();
    return title == null || title.isEmpty ? speaker : '$speaker · $title';
  }
}

/// A muted icon + text line with an optional trailing label (a bordered chip
/// for the category, a plain muted label for the hall).
class _MetaLine extends StatelessWidget {
  const _MetaLine({
    required this.icon,
    required this.text,
    this.trailingChip,
    this.chipBordered = true,
  });

  final IconData icon;
  final String text;
  final String? trailingChip;
  final bool chipBordered;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Icon(icon, size: 14, color: SimfTokens.beigeBorder),
        const SizedBox(width: SimfTokens.space1),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
        if (trailingChip != null && trailingChip!.isNotEmpty) ...<Widget>[
          const SizedBox(width: SimfTokens.space2),
          if (chipBordered)
            _CategoryChip(label: trailingChip!)
          else
            Text(
              trailingChip!,
              style: const TextStyle(
                color: SimfTokens.beigeBorder,
                fontSize: SimfTokens.textSm,
              ),
            ),
        ],
      ],
    );
  }
}

/// The bordered category pill (e.g. "تعليم والتنمية") on a session card.
class _CategoryChip extends StatelessWidget {
  const _CategoryChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textXs,
        ),
      ),
    );
  }
}
