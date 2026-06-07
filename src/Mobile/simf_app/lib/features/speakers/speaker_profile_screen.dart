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
import 'data/speaker_models.dart';
import 'data/speakers_repository.dart';
import 'speaker_initials.dart';

/// Page 020 — ملف المتحدث · Speaker profile (#20, `/speakers/:speakerId`, Guest+).
///
/// **Public** read (`GET /app/speakers/{id}`). Renders the navy hero (rank +
/// name), the large gold-ringed avatar, the four CV sections as a tab strip
/// (mockup `.sp-prof .tabs`/`.bio`), the speaker's sessions (tap → session
/// detail 17), the opted-in social links (only when `allowsDataSharing`) and the
/// **Request meeting** action (only when `allowsMeetingRequests`) — login-only
/// (`POST …/meeting-requests`, D-269): a guest is sent to sign-in, an approved
/// visitor gets the request form. UI is interim (avatar = initials) until the
/// SIMF-VID-001 pass.
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
    return Scaffold(
      appBar: AppBar(title: Text(l10n.speakerProfileTitle)),
      body: SafeArea(child: _buildBody(l10n)),
    );
  }

  Widget _buildBody(AppL10n l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_notFound) {
      return _Message(icon: Icons.person_off_outlined, text: l10n.speakerNotFound);
    }
    if (_error || _speaker == null) {
      return _ErrorState(message: l10n.speakerProfileError, onRetry: () => unawaited(_load()));
    }
    return _content(l10n, _speaker!);
  }

  Widget _content(AppL10n l10n, SpeakerDetail speaker) {
    final isArabic = l10n.isArabic;
    final country = speaker.localizedCountry(isArabic);
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
      padding: EdgeInsets.zero,
      children: <Widget>[
        _ProfileHeader(speaker: speaker, country: country, isArabic: isArabic),
        Padding(
          padding: const EdgeInsets.all(SimfTokens.space4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              if (sections.isNotEmpty) ...<Widget>[
                _CvTabs(
                  titles: sections.map((s) => s.title).toList(),
                  activeIndex: activeCv,
                  onSelect: (index) => setState(() => _activeCv = index),
                ),
                const SizedBox(height: SimfTokens.space4),
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
                for (final session in speaker.sessions)
                  _SessionRow(session: session, isArabic: isArabic),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

bool _has(String? value) => value != null && value.trim().isNotEmpty;

/// The navy hero band (mockup `.sp-prof .hero` + `.av-lg`): a centred gold rank
/// over the white name, then the large gold-ringed avatar.
class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({
    required this.speaker,
    required this.country,
    required this.isArabic,
  });

  final SpeakerDetail speaker;
  final String? country;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final sub = <String>[
      if (_has(speaker.rank)) speaker.rank!.trim(),
      if (country != null) country!,
    ];
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space6,
        SimfTokens.space4,
        SimfTokens.space5,
      ),
      decoration: const BoxDecoration(
        color: SimfTokens.navyDeep,
        border: Border(bottom: BorderSide(color: SimfTokens.line2)),
      ),
      child: Column(
        children: <Widget>[
          if (sub.isNotEmpty) ...<Widget>[
            Text(
              sub.join(' · '),
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: SimfTokens.accent,
                fontWeight: FontWeight.w600,
                fontSize: SimfTokens.textXs,
              ),
            ),
            const SizedBox(height: SimfTokens.space1),
          ],
          Text(
            speaker.localizedName(isArabic),
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: SimfTokens.surface,
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Container(
            width: 84,
            height: 84,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: SimfTokens.surface,
              shape: BoxShape.circle,
              border: Border.all(color: SimfTokens.accent, width: 2),
            ),
            child: Text(
              speakerInitials(speaker.localizedName(isArabic)),
              style: const TextStyle(
                color: SimfTokens.navy,
                fontWeight: FontWeight.w700,
                fontSize: SimfTokens.textXl,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The CV tab strip (mockup `.sp-prof .tabs`): bordered chips, the active one
/// filled gold with navy text. One chip per CV section that carries content.
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
          if (i > 0) const SizedBox(width: SimfTokens.space1),
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
      borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: SimfTokens.space1,
          vertical: SimfTokens.space2,
        ),
        decoration: BoxDecoration(
          color: selected ? SimfTokens.accent : Colors.transparent,
          borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
          border: Border.all(
            color: selected ? SimfTokens.accent : SimfTokens.line,
          ),
        ),
        child: Text(
          label,
          textAlign: TextAlign.center,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: selected ? SimfTokens.navy : SimfTokens.txtSecondary,
            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
            fontSize: SimfTokens.textXs,
            height: 1.2,
          ),
        ),
      ),
    );
  }
}

/// The CV body card (mockup `.sp-prof .bio .card`): white-4% fill, hairline
/// border, generous line-height for the long-form CV text.
class _CvCard extends StatelessWidget {
  const _CvCard({required this.body});

  final String body;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space4),
        child: Text(
          body,
          style: const TextStyle(
            color: SimfTokens.txtSecondary,
            fontSize: SimfTokens.textSm,
            height: 1.8,
          ),
        ),
      ),
    );
  }
}

class _SessionRow extends StatelessWidget {
  const _SessionRow({required this.session, required this.isArabic});

  final SpeakerSession session;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final hall = session.localizedHall(isArabic);
    return Card(
      margin: const EdgeInsets.only(bottom: SimfTokens.space2),
      clipBehavior: Clip.antiAlias,
      child: ListTile(
        onTap: () => context.pushNamed(
          RouteNames.sessionDetail,
          pathParameters: <String, String>{'sessionId': session.id},
        ),
        leading: const Icon(Icons.event_note_outlined, color: SimfTokens.accent),
        title: Text(
          session.localizedTitle(isArabic),
          style: const TextStyle(
            color: SimfTokens.surface,
            fontWeight: FontWeight.w600,
            fontSize: SimfTokens.textSm,
          ),
        ),
        subtitle: hall == null
            ? null
            : Text(
                hall,
                style: const TextStyle(
                  color: SimfTokens.txtSecondary,
                  fontSize: SimfTokens.textXs,
                ),
              ),
        trailing: const Icon(Icons.chevron_left, color: SimfTokens.txtTertiary),
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
        color: SimfTokens.txtTertiary,
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

class _Message extends StatelessWidget {
  const _Message({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 56, color: SimfTokens.txtTertiary),
          const SizedBox(height: SimfTokens.space3),
          Text(text, style: const TextStyle(color: SimfTokens.txtSecondary)),
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(SimfTokens.space6),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: SimfTokens.space4),
            FilledButton(onPressed: onRetry, child: Text(l10n.retryLabel)),
          ],
        ),
      ),
    );
  }
}
