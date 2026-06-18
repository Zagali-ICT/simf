import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:simf_auth_pkg/simf_auth_pkg.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/route_names.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../app/widgets/simf_svg_icon.dart';
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';
import 'speaker_initials.dart';

/// Page 020 — ملف المتحدث · Speaker profile (#20, `/speakers/:speakerId`,
/// Guest+), rebuilt to the KSA-Project Figma frame **908:2110 "About Speaker"**
/// on the shared navy shell.
///
/// **Public** read (`GET /app/speakers/{id}`) — behaviour unchanged. Frame
/// mapping: the two-line header (white name over the beige rank, the circled
/// back chevron), the 125px white circle ringed gold (the speaker's uploaded
/// SpeakerPhoto asset, initials fallback — D-357), the four CV tab pills in one
/// row (نبذة عنه /
/// المؤهلات العلمية / الخبرات التدريبية / الجوائز — the active pill filled gold
/// with white text, the rest `#192B41` with a beige hairline and beige text),
/// then the navy `#192B41` bio card with right-aligned white body text. Below
/// the frame's minimal content the screen keeps its full behaviour: the
/// **Request meeting** action (only when `allowsMeetingRequests`) — login-only
/// (`POST …/meeting-requests`, D-269): a guest is sent to sign-in, an approved
/// visitor gets the request form; the opted-in social links (only when
/// `allowsDataSharing`); and the speaker's sessions (tap → session detail 17).
class SpeakerProfileScreen extends ConsumerStatefulWidget {
  const SpeakerProfileScreen({required this.speakerId, super.key});

  final String speakerId;

  @override
  ConsumerState<SpeakerProfileScreen> createState() =>
      _SpeakerProfileScreenState();
}

class _SpeakerProfileScreenState extends ConsumerState<SpeakerProfileScreen> {
  bool _loading = true;
  bool _error = false;
  bool _notFound = false;
  SpeakerDetail? _speaker;
  int _activeCv = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = false;
      _notFound = false;
    });
    try {
      final speaker =
          await ref.read(speakersRepositoryProvider).getSpeaker(widget.speakerId);
      if (!mounted) {
        return;
      }
      setState(() {
        _speaker = speaker;
        _loading = false;
      });
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _loading = false;
        _notFound = failure.httpStatus == 404;
        _error = failure.httpStatus != 404;
      });
    }
  }

  void _onRequestMeeting(SpeakerDetail speaker, AppL10n l10n) {
    final auth = ref.read(authControllerProvider);
    if (auth is! AuthStateSignedIn) {
      // Login-only (E2) — send a guest to sign in (Page_020 L-5).
      context.pushNamed(RouteNames.signIn);
      return;
    }
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => _MeetingRequestSheet(
        speakerId: speaker.id,
        defaultName: auth.session.user.displayName,
        l10n: l10n,
      ),
    );
  }

  Future<void> _copyLink(String url, AppL10n l10n) async {
    final messenger = ScaffoldMessenger.of(context);
    await Clipboard.setData(ClipboardData(text: url));
    messenger.showSnackBar(SnackBar(content: Text(l10n.linkCopied)));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return KsaPage(
      onBack: () => ksaBackOrHome(context),
      header: _buildHeader(l10n),
      body: _buildBody(l10n),
    );
  }

  /// The frame's two-line title (white name over the beige rank) flanked by the
  /// circled back chevron — replaces the shell's default single-line title.
  Widget _buildHeader(AppL10n l10n) {
    final speaker = _speaker;
    final isArabic = l10n.isArabic;
    final title = speaker == null
        ? l10n.speakerProfileTitle
        : speaker.localizedName(isArabic);
    final rank = (speaker?.rank != null && speaker!.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : null;
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space3,
        vertical: SimfTokens.space2,
      ),
      child: Row(
        textDirection: TextDirection.ltr,
        children: <Widget>[
          SizedBox(
            width: 42,
            height: 42,
            child: KsaBackButton(onBack: () => ksaBackOrHome(context)),
          ),
          Expanded(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(
                  title,
                  textAlign: TextAlign.center,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: SimfTokens.textTitle,
                    fontWeight: FontWeight.w600,
                    color: Colors.white,
                  ),
                ),
                if (rank != null) ...<Widget>[
                  const SizedBox(height: SimfTokens.space1),
                  Text(
                    rank,
                    textAlign: TextAlign.center,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: SimfTokens.textMd,
                      fontWeight: FontWeight.w600,
                      color: SimfTokens.beigeBorder,
                    ),
                  ),
                ],
              ],
            ),
          ),
          // Balances the leading back button so the two-line title stays centred.
          const SizedBox(width: 42, height: 42),
        ],
      ),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_notFound) {
      return KsaEmptyState(
        icon: Icons.person_off_outlined,
        message: l10n.speakerNotFound,
      );
    }
    if (_error || _speaker == null) {
      return KsaErrorState(
        message: l10n.speakerProfileError,
        retryLabel: l10n.retryLabel,
        onRetry: () => unawaited(_load()),
      );
    }
    return _content(l10n, _speaker!);
  }

  Widget _content(AppL10n l10n, SpeakerDetail speaker) {
    final isArabic = l10n.isArabic;
    // The avatar builds `{base}/app/assets/SpeakerPhoto/{id}/image` for the
    // uploaded photo; the base already includes `/api/v1`.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    final sections = <_CvSection>[
      _CvSection(l10n.cvBio, speaker.localizedBio(isArabic)),
      _CvSection(l10n.cvQualifications, speaker.localizedQualifications(isArabic)),
      _CvSection(l10n.cvTraining, speaker.localizedTraining(isArabic)),
      _CvSection(l10n.cvAwards, speaker.localizedAwards(isArabic)),
    ].where((s) => s.body != null).toList();
    final activeCv =
        sections.isEmpty ? 0 : _activeCv.clamp(0, sections.length - 1);

    final socials = <_SocialLink>[
      if (speaker.allowsDataSharing && _has(speaker.facebookUrl))
        _SocialLink(Icons.facebook, speaker.facebookUrl!),
      if (speaker.allowsDataSharing && _has(speaker.linkedInUrl))
        _SocialLink(Icons.business_center_outlined, speaker.linkedInUrl!),
      if (speaker.allowsDataSharing && _has(speaker.xUrl))
        _SocialLink(Icons.alternate_email, speaker.xUrl!),
    ];

    return ListView(
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space8 + SimfTokens.space6, // 56px — matches Figma large gap above avatar
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        _Avatar(
          imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
          initials: speakerInitials(speaker.localizedName(isArabic)),
        ),
        const SizedBox(height: SimfTokens.space8 + SimfTokens.space2), // 40
        if (sections.isNotEmpty) ...<Widget>[
          _CvTabs(
            titles: sections.map((s) => s.title).toList(),
            activeIndex: activeCv,
            onSelect: (index) => setState(() => _activeCv = index),
          ),
          // Frame 912:2312 → 912:2331 — the CV card sits 24px below the tabs.
          const SizedBox(height: SimfTokens.space6), // 24
          _CvCard(body: sections[activeCv].body!),
        ],
        if (speaker.allowsMeetingRequests) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          FilledButton.icon(
            onPressed: () => _onRequestMeeting(speaker, l10n),
            icon: const Icon(Icons.handshake_outlined),
            label: Text(l10n.requestMeeting),
          ),
        ],
        if (socials.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space4),
          Wrap(
            spacing: SimfTokens.space2,
            children: <Widget>[
              for (final s in socials)
                ActionChip(
                  avatar: Icon(s.icon, size: 18),
                  label: Text(l10n.copyLinkLabel),
                  onPressed: () => unawaited(_copyLink(s.url, l10n)),
                ),
            ],
          ),
        ],
        if (speaker.sessions.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.speakerSessionsHeading),
          const SizedBox(height: SimfTokens.space3),
          for (final session in speaker.sessions) ...<Widget>[
            _SessionRow(session: session, isArabic: isArabic),
            const SizedBox(height: SimfTokens.space2),
          ],
        ],
      ],
    );
  }
}

bool _has(String? value) => value != null && value.trim().isNotEmpty;

/// The frame's identity avatar (908:2110 `912:2270`): a 125px white circle
/// ringed gold (2.77px). Renders the speaker's uploaded photo (the D-357
/// `SpeakerPhoto` asset) clipped to the circle, falling back to the speaker's
/// navy initials while it loads or when no photo is uploaded (the route 404s).
class _Avatar extends StatelessWidget {
  const _Avatar({required this.imageUrl, required this.initials});

  final String imageUrl;
  final String initials;

  @override
  Widget build(BuildContext context) {
    // The Figma placeholder is the gold SIMF anchor icon on white;
    // text initials are used only when the SVG itself fails to load.
    final placeholder = Center(
      child: SimfSvgIcon(
        'assets/icons/speaker_placeholder.svg',
        size: 64,
        color: SimfTokens.accent,
      ),
    );
    // The gold ring is the outer circle; a 2.77px pad reveals it around the
    // clipped white inner circle, so the photo sits inside the ring on white
    // (never painted under the gold stroke).
    return Center(
      child: Container(
        width: 125,
        height: 125,
        padding: const EdgeInsets.all(2.77),
        decoration: const BoxDecoration(
          color: SimfTokens.accent,
          shape: BoxShape.circle,
        ),
        child: Container(
          clipBehavior: Clip.antiAlias,
          decoration: const BoxDecoration(
            color: SimfTokens.surface,
            shape: BoxShape.circle,
          ),
          child: Image.network(
            imageUrl,
            fit: BoxFit.cover,
            gaplessPlayback: true,
            loadingBuilder: (context, child, progress) =>
                progress == null ? child : placeholder,
            errorBuilder: (context, error, stackTrace) => placeholder,
          ),
        ),
      ),
    );
  }
}

/// The CV tab strip (frame 908:2110 `912:2312`): a full-width row of equal
/// 48-high pills — the active one filled gold with white text, the rest navy
/// `#192B41` with a beige hairline and beige text. One pill per CV section that
/// carries content.
class _CvTabs extends StatelessWidget {
  const _CvTabs({
    required this.titles,
    required this.activeIndex,
    required this.onSelect,
  });

  final List<String> titles;
  final int activeIndex;
  final ValueChanged<int> onSelect;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        for (var i = 0; i < titles.length; i++) ...<Widget>[
          // Frame 912:2312 — four equal pills with an ~16px gap (SimfTokens.space4)
          if (i > 0) const SizedBox(width: SimfTokens.space4),
          Expanded(
            child: _CvTab(
              label: titles[i],
              selected: i == activeIndex,
              onTap: () => onSelect(i),
            ),
          ),
        ],
      ],
    );
  }
}

class _CvTab extends StatelessWidget {
  const _CvTab({
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        height: 48,
        alignment: Alignment.center,
        padding: const EdgeInsets.all(SimfTokens.space2),
        decoration: BoxDecoration(
          color: selected ? SimfTokens.accent : SimfTokens.navyDeep,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: selected
              ? null
              : Border.all(
                  color: SimfTokens.beigeBorder,
                  width: SimfTokens.hairline,
                ),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: selected ? Colors.white : SimfTokens.beigeBorder,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textSm,
            height: 1.2,
          ),
        ),
      ),
    );
  }
}

/// The CV body card (frame 908:2110 `912:2331`): the navy `#192B41` fill on the
/// 8px radius, right-aligned white body text at the frame's 21px line-height.
class _CvCard extends StatelessWidget {
  const _CvCard({required this.body});

  final String body;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space4,
      ),
      decoration: const BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
      ),
      child: Text(
        body,
        textAlign: TextAlign.start,
        style: const TextStyle(
          color: Colors.white,
          fontSize: SimfTokens.textMd,
          height: 1.5,
        ),
      ),
    );
  }
}

/// One of the speaker's sessions — kept on the navy card chrome (the frame's
/// minimal content stops at the bio, but the screen's behaviour does not).
class _SessionRow extends StatelessWidget {
  const _SessionRow({required this.session, required this.isArabic});

  final SpeakerSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final hall = session.localizedHall(isArabic);
    return KsaCard(
      onTap: () => context.pushNamed(
        RouteNames.sessionDetail,
        pathParameters: <String, String>{'sessionId': session.id},
      ),
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space3),
        child: Row(
          children: <Widget>[
            const Icon(Icons.event_note_outlined, color: SimfTokens.accent),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(
                    session.localizedTitle(isArabic),
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                      fontSize: SimfTokens.textSm,
                    ),
                  ),
                  if (hall != null) ...<Widget>[
                    const SizedBox(height: SimfTokens.space1),
                    Text(
                      hall,
                      style: const TextStyle(
                        color: SimfTokens.beigeBorder,
                        fontSize: SimfTokens.textXs,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            const SizedBox(width: SimfTokens.space2),
            const SimfSvgIcon(
              'assets/icons/ic_caret_left.svg',
              color: SimfTokens.txtTertiary,
              size: 18,
            ),
          ],
        ),
      ),
    );
  }
}

class _CvSection {
  const _CvSection(this.title, this.body);

  final String title;
  final String? body;
}

class _SocialLink {
  const _SocialLink(this.icon, this.url);

  final IconData icon;
  final String url;
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: SimfTokens.beigeBorder,
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textXs,
        letterSpacing: 0.8,
      ),
    );
  }
}

/// The meeting-request form (bottom sheet) — approved-account only (E2).
class _MeetingRequestSheet extends ConsumerStatefulWidget {
  const _MeetingRequestSheet({
    required this.speakerId,
    required this.defaultName,
    required this.l10n,
  });

  final String speakerId;
  final String defaultName;
  final AppL10n l10n;

  @override
  ConsumerState<_MeetingRequestSheet> createState() =>
      _MeetingRequestSheetState();
}

class _MeetingRequestSheetState extends ConsumerState<_MeetingRequestSheet> {
  late final TextEditingController _name =
      TextEditingController(text: widget.defaultName);
  final TextEditingController _subject = TextEditingController();
  bool _submitting = false;

  @override
  void dispose() {
    _name.dispose();
    _subject.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final l10n = widget.l10n;
    final name = _name.text.trim();
    final subject = _subject.text.trim();
    if (name.isEmpty || subject.isEmpty) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(l10n.meetingRequestInvalid)));
      return;
    }
    setState(() => _submitting = true);
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ref.read(speakersRepositoryProvider).submitMeetingRequest(
            widget.speakerId,
            requesterName: name,
            subject: subject,
          );
      if (!mounted) {
        return;
      }
      navigator.pop();
      messenger.showSnackBar(SnackBar(content: Text(l10n.meetingRequestSent)));
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _submitting = false);
      messenger.showSnackBar(
        SnackBar(content: Text(_failureText(failure, l10n))),
      );
    }
  }

  String _failureText(ApiFailure failure, AppL10n l10n) {
    if (failure.httpStatus == 409) {
      return l10n.meetingRequestNotAllowed;
    }
    if (failure.httpStatus == 400) {
      return l10n.meetingRequestInvalid;
    }
    return l10n.meetingRequestFailed;
  }

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    return Padding(
      padding: EdgeInsets.fromLTRB(
        SimfTokens.space5,
        0,
        SimfTokens.space5,
        MediaQuery.of(context).viewInsets.bottom + SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.requestMeeting,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          TextField(
            controller: _name,
            decoration: InputDecoration(labelText: l10n.meetingNameLabel),
            maxLength: 128,
          ),
          const SizedBox(height: SimfTokens.space2),
          TextField(
            controller: _subject,
            decoration: InputDecoration(labelText: l10n.meetingSubjectLabel),
            maxLength: 1000,
            maxLines: 3,
          ),
          const SizedBox(height: SimfTokens.space4),
          FilledButton(
            onPressed: _submitting ? null : () => unawaited(_submit()),
            child: Text(_submitting ? l10n.loadingLabel : l10n.meetingSendButton),
          ),
        ],
      ),
    );
  }
}
