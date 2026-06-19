import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
import '../venuemap/data/venue_map_models.dart';
import '../venuemap/data/venue_map_repository.dart';

/// Page 022 — الأجنحة · Booths (#22, `/booths`, Guest+), rebuilt to the
/// KSA-Project Figma frame **922:2458 "Halls"** on the shared KSA shell.
///
/// **Public.** Reuses the shipped booth reads (`GET /app/booths` + `/{id}`,
/// D-199 / D-230) already wired in [VenueMapRepository] — the list of exhibitor
/// booths; tapping a booth opens a bottom sheet with the lazily-loaded
/// description.
///
/// Frame mapping: the navy scaffold + centred header (الأجنحة) and the shared
/// bottom nav from [KsaPage]; a bordered search field (ابحث عن جناح أو شركة);
/// then one exhibitor card per booth — a company header row (short name + full
/// name beside the square logo tile, gold-hairline divider), the gold-bordered
/// **code pill** (A-12) beside the deep-navy **hall box**, the booth-officer row
/// + email / phone contact boxes (D-432 — now on the wire, server resolves the
/// officer Contact-first), and a **guide-me** gold CTA. P6 — D-440: the logo tile
/// renders the exhibitor's real `CompanyLogo` asset (D-357) via
/// `{base}/app/assets/CompanyLogo/{exhibitorContactId}/image`, falling back to
/// initials when the exhibitor has no linked Contact.
class BoothsScreen extends ConsumerStatefulWidget {
  const BoothsScreen({super.key});

  @override
  ConsumerState<BoothsScreen> createState() => _BoothsScreenState();
}

class _BoothsScreenState extends ConsumerState<BoothsScreen> {
  bool _loading = true;
  bool _error = false;
  List<BoothSummary> _booths = const <BoothSummary>[];
  String _query = '';

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
      final booths = await ref.read(venueMapRepositoryProvider).getBooths();
      if (!mounted) {
        return;
      }
      setState(() {
        _booths = booths;
        _loading = false;
      });
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = true;
        _loading = false;
      });
    }
  }

  Future<BoothDetail?> _safeDetail(String id) async {
    try {
      return await ref.read(venueMapRepositoryProvider).getBoothDetail(id);
    } on ApiFailure {
      return null;
    }
  }

  void _openBooth(BoothSummary booth) {
    final l10n = AppL10n.of(context);
    final detail = _safeDetail(booth.id);
    showModalBottomSheet<void>(
      context: context,
      backgroundColor: SimfTokens.navyDeep,
      showDragHandle: true,
      builder: (_) => _BoothSheet(l10n: l10n, booth: booth, detail: detail),
    );
  }

  // The booths whose name / exhibitor / sector / code matches the query
  // (client-side filter, mirroring the frame's local search field).
  List<BoothSummary> _filtered(bool isArabic) {
    final q = _query.trim().toLowerCase();
    if (q.isEmpty) {
      return _booths;
    }
    return _booths.where((booth) {
      final haystack = <String?>[
        booth.localizedName(isArabic),
        booth.localizedExhibitor(isArabic),
        booth.localizedSector(isArabic),
        booth.code,
      ].whereType<String>().join(' ').toLowerCase();
      return haystack.contains(q);
    }).toList(growable: false);
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      // Frame 922:2464 titles the screen "المعرض" (the nav tile/route stay "الأجنحة").
      title: l10n.boothsExhibitionTitle,
      onBack: () => ksaBackOrHome(context),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_error) {
      return KsaErrorState(
        message: l10n.boothsError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }

    final isArabic = l10n.isArabic;
    final filtered = _filtered(isArabic);
    // The card builds {base}/app/assets/CompanyLogo/{exhibitorContactId}/image.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;

    return Column(
      children: <Widget>[
        Padding(
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space2,
            SimfTokens.space4,
            0,
          ),
          child: _SearchField(
            l10n: l10n,
            onChanged: (value) => setState(() => _query = value),
          ),
        ),
        Expanded(
          child: _booths.isEmpty
              ? KsaEmptyState(
                  icon: Icons.storefront_outlined,
                  message: l10n.boothsEmpty,
                )
              : filtered.isEmpty
                  ? KsaEmptyState(
                      icon: Icons.search_off_outlined,
                      message: l10n.boothsNoMatch,
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.fromLTRB(
                        SimfTokens.space4,
                        SimfTokens.space4,
                        SimfTokens.space4,
                        SimfTokens.space6,
                      ),
                      itemCount: filtered.length,
                      separatorBuilder: (_, __) =>
                          const SizedBox(height: SimfTokens.space4),
                      itemBuilder: (context, index) => _BoothCard(
                        booth: filtered[index],
                        l10n: l10n,
                        isArabic: isArabic,
                        baseUrl: baseUrl,
                        onTap: () => _openBooth(filtered[index]),
                      ),
                    ),
        ),
      ],
    );
  }
}

/// The bordered search field (frame node 922:2549): a navy box with the
/// beige bold hairline, the muted hint, and a leading (inline-start) search
/// glyph.
class _SearchField extends StatelessWidget {
  const _SearchField({required this.l10n, required this.onChanged});

  final AppL10n l10n;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextField(
      onChanged: onChanged,
      textInputAction: TextInputAction.search,
      style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textSm),
      decoration: InputDecoration(
        isDense: true,
        filled: true,
        fillColor: SimfTokens.navyDeep,
        hintText: l10n.boothsSearchHint,
        hintStyle: const TextStyle(
          color: SimfTokens.beigeBorder,
          fontSize: SimfTokens.textSm,
        ),
        // Frame 922:2549 — the search glyph sits at the inline start (physical
        // right under RTL), so it is a prefix, not a suffix.
        prefixIcon: const SimfSvgIcon(
          'assets/icons/ic_search.svg',
          size: 18,
          color: Colors.white,
        ),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space3,
          vertical: SimfTokens.space4,
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(
            color: SimfTokens.beigeBorder,
            width: SimfTokens.hairlineBold,
          ),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
      ),
    );
  }
}

/// One exhibitor card (frame node 922:2554): a navy box with the beige
/// hairline carrying — top to bottom — the company header (short name + full
/// name beside the square logo tile, over a gold hairline), the gold **code
/// pill** beside the deep-navy **hall box**, the booth-officer row + email /
/// phone contact boxes (D-432, shown only when the wire carries them), and a
/// full-width gold **guide-me** CTA. P6 — D-440: the logo tile renders the
/// exhibitor's real CompanyLogo (D-357), initials fallback.
class _BoothCard extends StatelessWidget {
  const _BoothCard({
    required this.booth,
    required this.l10n,
    required this.isArabic,
    required this.baseUrl,
    required this.onTap,
  });

  final BoothSummary booth;
  final AppL10n l10n;
  final bool isArabic;
  final String baseUrl;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return KsaCard(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space2),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            _CompanyHeader(booth: booth, isArabic: isArabic, baseUrl: baseUrl),
            const SizedBox(height: SimfTokens.space4),
            _HallRow(booth: booth, l10n: l10n),
            // D-432 — the booth-officer row + email/phone boxes (now on the
            // wire, server resolves the officer Contact-first); shown only when
            // the booth actually carries that contact data.
            if ((booth.officerName ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _OfficerRow(name: booth.officerName!.trim(), l10n: l10n),
            ],
            if ((booth.officerEmail ?? '').trim().isNotEmpty ||
                (booth.officerPhone ?? '').trim().isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _ContactBoxes(
                email: booth.officerEmail?.trim(),
                phone: booth.officerPhone?.trim(),
              ),
            ],
            if (booth.code.isNotEmpty) ...<Widget>[
              const SizedBox(height: SimfTokens.space4),
              _GuideButton(code: booth.code, l10n: l10n),
            ],
          ],
        ),
      ),
    );
  }
}

/// The card header (frame node 922:2789): the square logo-initials tile on the
/// inline start (physical right) with the company short name (gold) over its
/// full name (beige) beside it, under a gold hairline rule — matching the
/// frame's logo-right / text-left RTL layout.
class _CompanyHeader extends StatelessWidget {
  const _CompanyHeader({
    required this.booth,
    required this.isArabic,
    required this.baseUrl,
  });

  final BoothSummary booth;
  final bool isArabic;
  final String baseUrl;

  @override
  Widget build(BuildContext context) {
    final name = booth.localizedName(isArabic);
    final fullName = booth.localizedExhibitor(isArabic);
    return Container(
      padding: const EdgeInsets.only(bottom: SimfTokens.space2),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.accent,
            width: SimfTokens.hairline,
          ),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          _LogoTile(
            contactId: booth.exhibitorContactId,
            baseUrl: baseUrl,
            initials: _initials(name),
          ),
          const SizedBox(width: SimfTokens.space2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  name,
                  style: const TextStyle(
                    color: SimfTokens.accent,
                    fontWeight: FontWeight.w500,
                    fontSize: SimfTokens.textMd,
                  ),
                ),
                if (fullName != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space2),
                  Text(
                    fullName,
                    style: const TextStyle(
                      color: SimfTokens.beigeBorder,
                      fontWeight: FontWeight.w400,
                      fontSize: SimfTokens.textSm,
                      height: 1.3,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// The square company-logo tile (frame node 922:2793): a 48×48 navy square with
/// a beige hairline. P6 — D-440: renders the exhibitor's real CompanyLogo (the
/// D-357 asset owned by [contactId]) clipped to fill, falling back to the booth
/// initials while it loads or when the exhibitor has no linked Contact / logo.
class _LogoTile extends StatelessWidget {
  const _LogoTile({
    required this.contactId,
    required this.baseUrl,
    required this.initials,
  });

  final String? contactId;
  final String baseUrl;
  final String initials;

  @override
  Widget build(BuildContext context) {
    final fallback = Text(
      initials,
      textDirection: TextDirection.ltr,
      style: const TextStyle(
        color: Colors.white,
        fontWeight: FontWeight.w600,
        fontSize: SimfTokens.textMd,
      ),
    );
    final id = contactId?.trim() ?? '';
    return Container(
      width: 48,
      height: 48,
      alignment: Alignment.center,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: id.isEmpty
          ? fallback
          : Image.network(
              '$baseUrl/app/assets/CompanyLogo/$id/image',
              width: 48,
              height: 48,
              fit: BoxFit.cover,
              gaplessPlayback: true,
              loadingBuilder: (context, child, progress) =>
                  progress == null ? child : fallback,
              errorBuilder: (context, error, stackTrace) => fallback,
            ),
    );
  }
}

/// The hall row (frame node 922:2795): the gold-bordered **code pill** (A-12)
/// beside the deep-navy **hall box**. The frame's hall box reads
/// "HALL A · القاعة الرئيسية"; the wire carries no hall **name**, so the box
/// shows the booth's sector when present, else a generic "exhibition hall"
/// label — never an invented hall name (D11 / Page_015 L-6).
class _HallRow extends StatelessWidget {
  const _HallRow({required this.booth, required this.l10n});

  final BoothSummary booth;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    // D-432 — prefer the real hall display name (now on the wire), then the
    // booth sector, then the generic fallback (D11/Page_015 L-6 — never invent).
    final hallLabel = booth.localizedHallName(l10n.isArabic) ??
        booth.localizedSector(l10n.isArabic) ??
        l10n.boothsHallFallback;
    final code = booth.code;
    // Both children are a fixed 48 high; the row must NOT stretch — inside the
    // card Column (unbounded height) CrossAxisAlignment.stretch would size the
    // row to infinite height and crash layout. Centre-align the two 48px boxes.
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        if (code.isNotEmpty) ...<Widget>[
          _CodePill(code: code),
          const SizedBox(width: SimfTokens.space4),
        ],
        Expanded(
          child: _HallBox(label: hallLabel),
        ),
      ],
    );
  }
}

/// The accent booth-number pill (frame node 922:2796): a 109-wide navy box with
/// a gold hairline, the code in gold, always LTR.
class _CodePill extends StatelessWidget {
  const _CodePill({required this.code});

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 109,
      height: 48,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.accent,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        code,
        textDirection: TextDirection.ltr,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w600,
          fontSize: SimfTokens.textMd,
        ),
      ),
    );
  }
}

/// The deep-navy hall box (frame node 922:2798): a 48-high `#01132D` box with a
/// beige hairline, the label centred in gold.
class _HallBox extends StatelessWidget {
  const _HallBox({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 48,
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navy,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Text(
        label,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
        textAlign: TextAlign.center,
        style: const TextStyle(
          color: SimfTokens.accent,
          fontWeight: FontWeight.w600,
          fontSize: SimfTokens.textMd,
        ),
      ),
    );
  }
}

/// The booth-officer row (frame 922:2800): the officer's gold name over the
/// fixed beige role label, beside a gold initials tile (e.g. "RS"). D-432 — the
/// officer contact now ships on the wire (server resolves it Contact-first).
class _OfficerRow extends StatelessWidget {
  const _OfficerRow({required this.name, required this.l10n});

  final String name;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: SimfTokens.accent,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
              const SizedBox(height: SimfTokens.space1),
              Text(
                l10n.boothsOfficerRole,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  fontSize: SimfTokens.textXs,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: SimfTokens.space2),
        Container(
          width: 40,
          height: 40,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.accent,
            borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          ),
          child: Text(
            _initials(name),
            textDirection: TextDirection.ltr,
            style: const TextStyle(
              color: SimfTokens.navy,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textSm,
            ),
          ),
        ),
      ],
    );
  }
}

/// The booth-officer contact boxes (frame 922:2810): email + phone in two
/// bordered navy boxes side by side, each with a trailing glyph. D-432 — only
/// the boxes the wire actually carries are shown.
class _ContactBoxes extends StatelessWidget {
  const _ContactBoxes({this.email, this.phone});

  final String? email;
  final String? phone;

  @override
  Widget build(BuildContext context) {
    final hasEmail = (email ?? '').isNotEmpty;
    final hasPhone = (phone ?? '').isNotEmpty;
    return Row(
      children: <Widget>[
        if (hasEmail)
          Expanded(child: _ContactBox(text: email!, icon: Icons.mail_outline)),
        if (hasEmail && hasPhone) const SizedBox(width: SimfTokens.space4),
        if (hasPhone)
          Expanded(child: _ContactBox(text: phone!, icon: Icons.call_outlined)),
      ],
    );
  }
}

class _ContactBox extends StatelessWidget {
  const _ContactBox({required this.text, required this.icon});

  final String text;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: SimfTokens.space2),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border.all(
          color: SimfTokens.beigeBorder,
          width: SimfTokens.hairline,
        ),
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textDirection: TextDirection.ltr,
              style: const TextStyle(
                color: Colors.white,
                fontSize: SimfTokens.textXs,
              ),
            ),
          ),
          const SizedBox(width: SimfTokens.space2),
          Icon(icon, size: 16, color: SimfTokens.beigeBorder),
        ],
      ),
    );
  }
}

/// The full-width gold guide-me CTA (frame node 922:2820): "أرشدني إلى الجناح ·
/// A-12" with a trailing location glyph, white text on the gold fill. Tapping it
/// opens the same booth sheet as the card (no map deep-link is wired in this
/// re-skin — the venue map carries the booth positions on its own page).
class _GuideButton extends StatelessWidget {
  const _GuideButton({required this.code, required this.l10n});

  final String code;
  final AppL10n l10n;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: SimfTokens.accent,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space4,
          vertical: SimfTokens.space3,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Flexible(
              child: Text(
                l10n.boothsGuideMe(code),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w600,
                  fontSize: SimfTokens.textSm,
                ),
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            const SimfSvgIcon(
              'assets/icons/nav_location.svg',
              size: 18,
              color: Colors.white,
            ),
          ],
        ),
      ),
    );
  }
}

/// The first two letters of a booth name, upper-cased, for the logo tile.
String _initials(String name) {
  final trimmed = name.trim();
  if (trimmed.isEmpty) {
    return '';
  }
  return trimmed.substring(0, trimmed.length >= 2 ? 2 : 1).toUpperCase();
}

/// The booth detail bottom sheet — the company name + sub-line over the
/// lazily-loaded description paragraph, on the navy surface.
class _BoothSheet extends StatelessWidget {
  const _BoothSheet({
    required this.l10n,
    required this.booth,
    required this.detail,
  });

  final AppL10n l10n;
  final BoothSummary booth;
  final Future<BoothDetail?> detail;

  @override
  Widget build(BuildContext context) {
    final isArabic = l10n.isArabic;
    final exhibitor = booth.localizedExhibitor(isArabic);
    final sector = booth.localizedSector(isArabic);
    final parts = <String>[
      if (exhibitor != null) exhibitor,
      if (sector != null) sector,
    ];
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space5,
        0,
        SimfTokens.space5,
        SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            booth.localizedName(isArabic),
            style: const TextStyle(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
          if (parts.isNotEmpty) ...<Widget>[
            const SizedBox(height: SimfTokens.space2),
            Text(
              parts.join(' · '),
              style: const TextStyle(color: SimfTokens.beigeBorder),
            ),
          ],
          const SizedBox(height: SimfTokens.space3),
          FutureBuilder<BoothDetail?>(
            future: detail,
            builder: (context, snapshot) {
              if (snapshot.connectionState == ConnectionState.waiting) {
                return Text(
                  l10n.loadingLabel,
                  style: const TextStyle(color: SimfTokens.txtTertiary),
                );
              }
              final description = snapshot.data?.localizedDescription(isArabic);
              if (description == null) {
                return const SizedBox.shrink();
              }
              return Text(
                description,
                style: const TextStyle(
                  color: SimfTokens.beigeBorder,
                  height: 1.5,
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}
