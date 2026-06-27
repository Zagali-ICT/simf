import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import 'data/delegation_models.dart';
import 'data/delegations_repository.dart';

// Pre-baked translucent fills (alpha folded into the ARGB so the app stays off
// the deprecated Color.withOpacity API) — straight from the Figma 1426:10771
// tokens: gold #C9A84C @ 7 % (grid), @ 6 % (head box fill), @ 15 % (head box
// border), and beige #C2B8A2 @ 10 % (the chips).
const Color _gridLine = Color(0x12C9A84C);
const Color _headBoxFill = Color(0x0FC9A84C);
const Color _headBoxBorder = Color(0x26C9A84C);
const Color _chipFill = Color(0x1AC2B8A2);

/// The Delegations screen — App "الوفود" (Figma 1426:10771): the invited
/// countries' delegations, each card showing the flag, country name, head of
/// delegation (name + role), the date range and member count, topped by a
/// stats strip (participating countries + total participants) and a search box.
/// Public (anonymous `GET /app/delegations`).
class DelegationsScreen extends ConsumerStatefulWidget {
  const DelegationsScreen({super.key});

  @override
  ConsumerState<DelegationsScreen> createState() => _DelegationsScreenState();
}

class _DelegationsScreenState extends ConsumerState<DelegationsScreen> {
  final TextEditingController _searchController = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = Directionality.of(context) == TextDirection.rtl;
    final delegations = ref.watch(delegationsProvider);

    return KsaPage(
      title: l10n.delegationsTitle,
      onBack: () => ksaBackOrHome(context),
      body: delegations.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: SimfTokens.accent),
        ),
        error: (_, __) => KsaErrorState(
          message: l10n.delegationsError,
          retryLabel: l10n.retryLabel,
          onRetry: () => ref.invalidate(delegationsProvider),
        ),
        data: (data) => _DelegationsBody(
          data: data,
          query: _query,
          isArabic: isArabic,
          l10n: l10n,
          searchController: _searchController,
          onQueryChanged: (value) => setState(() => _query = value),
        ),
      ),
    );
  }
}

class _DelegationsBody extends StatelessWidget {
  const _DelegationsBody({
    required this.data,
    required this.query,
    required this.isArabic,
    required this.l10n,
    required this.searchController,
    required this.onQueryChanged,
  });

  final Delegations data;
  final String query;
  final bool isArabic;
  final AppL10n l10n;
  final TextEditingController searchController;
  final ValueChanged<String> onQueryChanged;

  @override
  Widget build(BuildContext context) {
    final filtered =
        data.items.where((item) => item.matches(query)).toList(growable: false);
    final flags = data.items
        .map((item) => item.flagEmoji)
        .where((flag) => flag.isNotEmpty)
        .toList(growable: false);

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _StatsStrip(
          countryCount: data.countryCount,
          totalParticipants: data.totalParticipants,
          flags: flags,
          l10n: l10n,
        ),
        const SizedBox(height: SimfTokens.space4),
        _SearchField(
          controller: searchController,
          hint: l10n.delegationsSearchHint,
          onChanged: onQueryChanged,
        ),
        const SizedBox(height: SimfTokens.space4),
        if (filtered.isEmpty)
          Padding(
            padding: const EdgeInsets.only(top: SimfTokens.space8),
            child: KsaEmptyState(
              icon: Icons.flag_outlined,
              message: data.items.isEmpty
                  ? l10n.delegationsEmpty
                  : l10n.delegationsNoResults,
            ),
          )
        else
          for (final item in filtered) ...<Widget>[
            _DelegationCard(item: item, isArabic: isArabic, l10n: l10n),
            const SizedBox(height: SimfTokens.space3),
          ],
      ],
    );
  }
}

/// The header stats strip (Figma 1426:10781): a navy card with a faint gold
/// grid, the invited countries' flags scattered across it, and the two big-gold
/// stats — participating countries (left) + total participants (right).
class _StatsStrip extends StatelessWidget {
  const _StatsStrip({
    required this.countryCount,
    required this.totalParticipants,
    required this.flags,
    required this.l10n,
  });

  final int countryCount;
  final int totalParticipants;
  final List<String> flags;
  final AppL10n l10n;

  /// The Figma's decorative scatter positions (relative to the strip), filled
  /// with the real invited-country flags so the map reflects the data.
  static const List<Offset> _spots = <Offset>[
    Offset(0.60, 0.30),
    Offset(0.16, 0.25),
    Offset(0.42, 0.10),
    Offset(0.46, 0.16),
    Offset(0.48, 0.09),
    Offset(0.76, 0.22),
    Offset(0.63, 0.30),
    Offset(0.54, 0.18),
  ];

  @override
  Widget build(BuildContext context) {
    final count = flags.length < _spots.length ? flags.length : _spots.length;
    return Container(
      height: 100,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        borderRadius: BorderRadius.circular(14),
      ),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth;
          final height = constraints.maxHeight;
          return Stack(
            children: <Widget>[
              const Positioned.fill(
                child: CustomPaint(painter: _GridPainter()),
              ),
              for (var i = 0; i < count; i++)
                Positioned(
                  left: _spots[i].dx * width,
                  top: _spots[i].dy * height,
                  child: Text(
                    flags[i],
                    style: const TextStyle(fontSize: 14),
                  ),
                ),
              Positioned(
                left: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: _Stat(
                  value: countryCount,
                  label: l10n.delegationsCountriesStat,
                  alignEnd: false,
                ),
              ),
              Positioned(
                right: SimfTokens.space4,
                bottom: SimfTokens.space3,
                child: _Stat(
                  value: totalParticipants,
                  label: l10n.delegationsParticipantsStat,
                  alignEnd: true,
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _GridPainter extends CustomPainter {
  const _GridPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = _gridLine
      ..strokeWidth = 1;
    for (var i = 1; i < 7; i++) {
      final x = size.width * i / 7;
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
    for (var i = 1; i < 5; i++) {
      final y = size.height * i / 5;
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
  }

  @override
  bool shouldRepaint(_GridPainter oldDelegate) => false;
}

class _Stat extends StatelessWidget {
  const _Stat({
    required this.value,
    required this.label,
    required this.alignEnd,
  });

  final int value;
  final String label;
  final bool alignEnd;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment:
          alignEnd ? CrossAxisAlignment.end : CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          '$value',
          style: const TextStyle(
            fontSize: SimfTokens.textXl,
            fontWeight: FontWeight.w700,
            color: SimfTokens.accent,
          ),
        ),
        Text(
          label,
          style: const TextStyle(
            fontSize: SimfTokens.textSm,
            color: SimfTokens.beigeBorder,
          ),
        ),
      ],
    );
  }
}

/// The search box (Figma 1426:10819): a dark rounded field with a filter glyph
/// at the inline start and a search glyph at the inline end.
class _SearchField extends StatelessWidget {
  const _SearchField({
    required this.controller,
    required this.hint,
    required this.onChanged,
  });

  final TextEditingController controller;
  final String hint;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    // Figma 1426:10819: the search glyph + hint sit at the inline start (right
    // in RTL) and the filter glyph in a 40px divider-walled cell at the inline
    // end (left in RTL). radius-4, beige hairline.
    return Container(
      height: 48,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
      ),
      child: Row(
        children: <Widget>[
          const Padding(
            padding: EdgeInsetsDirectional.only(start: SimfTokens.space3),
            child: Icon(Icons.search, color: SimfTokens.beigeBorder, size: 16),
          ),
          Expanded(
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              style: const TextStyle(color: Colors.white, fontSize: 14),
              cursorColor: SimfTokens.accent,
              decoration: InputDecoration(
                hintText: hint,
                hintStyle: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: 14,
                ),
                isDense: true,
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: SimfTokens.space3,
                  vertical: SimfTokens.space2,
                ),
              ),
            ),
          ),
          Container(
            width: 40,
            // Fill the 48-high field so the divider is a full-height wall, not a
            // short stub (a bare Row centres children on the cross axis).
            height: double.infinity,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              border: BorderDirectional(
                start: BorderSide(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
              ),
            ),
            child: const Icon(Icons.tune, color: SimfTokens.beigeBorder, size: 16),
          ),
        ],
      ),
    );
  }
}

/// One delegation card (Figma 1426:10838): country identity, the head-of-
/// delegation box (when set), and the date range + member count.
class _DelegationCard extends StatelessWidget {
  const _DelegationCard({
    required this.item,
    required this.isArabic,
    required this.l10n,
  });

  final DelegationItem item;
  final bool isArabic;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      radius: SimfTokens.radius, // 8 (Figma 1426:10838)
      borderWidth: 0, // borderless
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            _identityRow(),
            if (item.hasHead) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _headBox(),
            ],
            const SizedBox(height: SimfTokens.space4),
            _bottomRow(),
          ],
        ),
      ),
    );
  }

  Widget _identityRow() {
    final title = item.localizedCountry(isArabic);
    final subtitle = item.localizedCountrySubtitle(isArabic);
    // Only show the other-language name when it actually adds information — when
    // a country has just one name the title falls back to it, so the subtitle
    // would otherwise duplicate the title.
    final showSubtitle = subtitle.isNotEmpty && subtitle != title;
    return Row(
      children: <Widget>[
        _FlagBox(emoji: item.flagEmoji),
        const SizedBox(width: SimfTokens.space3),
        Expanded(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: Colors.white,
                ),
              ),
              if (showSubtitle) ...<Widget>[
                const SizedBox(height: SimfTokens.space2), // 8 (Figma 1426:10840)
                Text(
                  subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: SimfTokens.textSm,
                    fontWeight: FontWeight.w500,
                    color: SimfTokens.beigeBorder,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }

  Widget _headBox() {
    return Container(
      decoration: BoxDecoration(
        color: _headBoxFill,
        border: Border.all(color: _headBoxBorder),
        borderRadius: BorderRadius.circular(10),
      ),
      padding: const EdgeInsets.all(9),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Row(
              children: <Widget>[
                _HeadAvatar(initial: item.headInitial(isArabic)),
                const SizedBox(width: SimfTokens.space2),
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        item.localizedHead(isArabic) ?? '',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: SimfTokens.accent,
                        ),
                      ),
                      if (item.headTitle != null &&
                          item.headTitle!.trim().isNotEmpty) ...<Widget>[
                        const SizedBox(height: 2),
                        Text(
                          item.headTitle!.trim(),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.w500,
                            color: SimfTokens.beigeBorder,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          _HeadChip(label: l10n.delegationsHeadLabel),
        ],
      ),
    );
  }

  Widget _bottomRow() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: <Widget>[
        // Flexible so a long member label / date range ellipsizes instead of
        // overflowing on a narrow device or with a large OS font scale.
        Flexible(child: _MemberChip(text: l10n.delegationsMembers(item.memberCount))),
        if (item.hasDateRange)
          Flexible(
            child: _DateGroup(
              text:
                  l10n.delegationsDateRange(item.arrivalDate, item.departureDate),
            ),
          )
        else
          const SizedBox.shrink(),
      ],
    );
  }
}

class _FlagBox extends StatelessWidget {
  const _FlagBox({required this.emoji});

  final String emoji;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.surfaceTint,
        border: Border.all(color: SimfTokens.line),
        borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
      ),
      child: Text(emoji, style: const TextStyle(fontSize: 28)),
    );
  }
}

class _HeadAvatar extends StatelessWidget {
  const _HeadAvatar({required this.initial});

  final String initial;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 38,
      height: 38,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        initial,
        style: const TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: Colors.white,
        ),
      ),
    );
  }
}

class _HeadChip extends StatelessWidget {
  const _HeadChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: _chipFill,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space2,
        vertical: SimfTokens.space1,
      ),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: 9,
          fontWeight: FontWeight.w700,
          color: SimfTokens.accent,
        ),
      ),
    );
  }
}

class _MemberChip extends StatelessWidget {
  const _MemberChip({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: _chipFill,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      padding: const EdgeInsets.all(SimfTokens.space2),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Flexible(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: SimfTokens.beigeBorder,
              ),
            ),
          ),
          const SizedBox(width: 6),
          const Icon(
            Icons.groups_outlined,
            size: 12,
            color: SimfTokens.beigeBorder,
          ),
        ],
      ),
    );
  }
}

class _DateGroup extends StatelessWidget {
  const _DateGroup({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Flexible(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 10,
              fontWeight: FontWeight.w500,
              color: SimfTokens.beigeBorder,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space1),
        const Icon(Icons.schedule, size: 12, color: SimfTokens.beigeBorder),
      ],
    );
  }
}
