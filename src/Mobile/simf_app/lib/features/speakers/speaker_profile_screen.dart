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
import '../../app/widgets/simf_page_shell.dart';
import '../account/data/profile_repository.dart';
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';
import 'speaker_initials.dart';
import 'widgets/meeting_request_sheet.dart';
import 'widgets/speaker_avatar.dart';
import 'widgets/speaker_cv.dart';
import 'widgets/speaker_profile_header.dart';
import 'widgets/speaker_sessions.dart';

/// Page 020 — ملف المتحدث · Speaker profile (#20, `/speakers/:speakerId`,
/// Guest+), rebuilt to the KSA-Project Figma frame **908:2110 "About Speaker"**
/// on the shared navy shell.
///
/// **Public** read (`GET /app/speakers/{id}`) — behaviour unchanged. Frame
/// mapping: the two-line header (`SpeakerProfileHeader`), the 125px gold-ringed
/// avatar (`SpeakerAvatar`, D-357 photo + placeholder), the four CV tab pills
/// (`SpeakerCvTabs`) over the navy bio card (`SpeakerCvCard`). Below the frame's
/// minimal content the screen keeps its full behaviour: the **Request meeting**
/// action (only when `allowsMeetingRequests` **and the viewer is a VIP tier**,
/// D-729) — login-only (`POST …/meeting-requests`, D-269), opening the
/// `MeetingRequestSheet` (the endpoint enforces the same VIP rule); the
/// opted-in social links (only when `allowsDataSharing`); and the speaker's
/// sessions (`SpeakerSessionRow`, tap → session detail 17).
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
      // Figma 1776:4958 — a light "طلب مقابلة" sheet with its own gold drag
      // handle (so the default grey handle is off) and rounded top corners.
      backgroundColor: SimfTokens.cardBeige,
      showDragHandle: false,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(SimfTokens.radius)),
      ),
      builder: (_) => MeetingRequestSheet(
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
    final speaker = _speaker;
    final isArabic = l10n.isArabic;
    final rank = (speaker?.rank != null && speaker!.rank!.trim().isNotEmpty)
        ? speaker.rank!.trim()
        : null;
    return SimfPageShell(
      onBack: () => backOrHome(context),
      header: SpeakerProfileHeader(
        title: speaker == null
            ? l10n.speakerProfileTitle
            : speaker.localizedName(isArabic),
        rank: rank,
        flag: speaker?.flagEmoji ?? '',
        onBack: () => backOrHome(context),
      ),
      body: _buildBody(l10n),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(color: SimfTokens.accent),
      );
    }
    if (_notFound) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: SimfEmptyState(
            icon: Icons.person_off_outlined,
            message: l10n.speakerNotFound,
          ),
        ),
      );
    }
    if (_error || _speaker == null) {
      return SimfPullToRefresh(
        onRefresh: _load,
        child: SimfPullableHost(
          child: SimfErrorState(
            message: l10n.speakerProfileError,
            retryLabel: l10n.retryLabel,
            onRetry: () => unawaited(_load()),
          ),
        ),
      );
    }
    return SimfPullToRefresh(
      onRefresh: _load,
      child: _content(l10n, _speaker!),
    );
  }

  Widget _content(AppL10n l10n, SpeakerDetail speaker) {
    final isArabic = l10n.isArabic;
    // The avatar builds `{base}/app/assets/SpeakerPhoto/{id}/image` for the
    // uploaded photo; the base already includes `/api/v1`.
    final baseUrl = ref.watch(simfDataConfigProvider).baseUrl;
    // D-729 (owner item 15) — requesting a speaker meeting is VIP-only; false
    // while the profile loads / for guests / non-VIP (the endpoint also gates).
    final isVip = ref.watch(currentUserIsVipProvider).value ?? false;
    final sections = <SpeakerCvSection>[
      SpeakerCvSection(l10n.cvBio, speaker.localizedBio(isArabic)),
      SpeakerCvSection(
        l10n.cvQualifications,
        speaker.localizedQualifications(isArabic),
      ),
      SpeakerCvSection(l10n.cvTraining, speaker.localizedTraining(isArabic)),
      SpeakerCvSection(l10n.cvAwards, speaker.localizedAwards(isArabic)),
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
      // D-544 — the opted-in personal/professional website, shown as a 4th
      // chip alongside the socials (the field postdates the 908-2110 frame).
      if (speaker.allowsDataSharing && _has(speaker.websiteUrl))
        _SocialLink(Icons.language, speaker.websiteUrl!),
    ];

    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space8 + SimfTokens.space6, // 56px — matches Figma large gap above avatar
        SimfTokens.space4,
        SimfTokens.space6,
      ),
      children: <Widget>[
        SpeakerAvatar(
          imageUrl: '$baseUrl/app/assets/SpeakerPhoto/${speaker.id}/image',
          initials: speakerInitials(speaker.localizedName(isArabic)),
        ),
        const SizedBox(height: SimfTokens.space8 + SimfTokens.space2), // 40
        if (sections.isNotEmpty) ...<Widget>[
          SpeakerCvTabs(
            titles: sections.map((s) => s.title).toList(),
            activeIndex: activeCv,
            onSelect: (index) => setState(() => _activeCv = index),
          ),
          // Frame 912:2312 → 912:2331 — the CV card sits 24px below the tabs.
          const SizedBox(height: SimfTokens.space6), // 24
          SpeakerCvCard(body: sections[activeCv].body!),
        ],
        // D-729 (owner item 15) — VIP-only: only VVIP/VIP guests see the
        // "request meeting" CTA (the endpoint enforces the same rule).
        if (speaker.allowsMeetingRequests && isVip) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          // Figma 1049:2302 — a text-only gold CTA (no leading icon).
          FilledButton(
            onPressed: () => _onRequestMeeting(speaker, l10n),
            child: Text(l10n.requestMeeting),
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
          SpeakerSectionHeading(l10n.speakerSessionsHeading),
          const SizedBox(height: SimfTokens.space3),
          for (final session in speaker.sessions) ...<Widget>[
            SpeakerSessionRow(session: session, isArabic: isArabic),
            const SizedBox(height: SimfTokens.space2),
          ],
        ],
      ],
    );
  }
}

bool _has(String? value) => value != null && value.trim().isNotEmpty;

class _SocialLink {
  const _SocialLink(this.icon, this.url);

  final IconData icon;
  final String url;
}
