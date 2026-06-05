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

/// Page 020 — ملف متحدث · Speaker profile (#20, `/speakers/:speakerId`, Guest+).
///
/// **Public** read (`GET /app/speakers/{id}`). Renders the header, the four CV
/// sections, the speaker's sessions (tap → session detail 17), the opted-in
/// social links (only when `allowsDataSharing`) and the **Request meeting**
/// action (only when `allowsMeetingRequests`) — login-only (`POST …/meeting-requests`,
/// D-269): a guest is sent to sign-in, an approved visitor gets the request form.
/// UI is interim (avatar = initials; CV as stacked sections, not tabs) until the
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

    final socials = <_SocialLink>[
      if (speaker.allowsDataSharing && _has(speaker.facebookUrl))
        _SocialLink(Icons.facebook, speaker.facebookUrl!),
      if (speaker.allowsDataSharing && _has(speaker.linkedInUrl))
        _SocialLink(Icons.business_center_outlined, speaker.linkedInUrl!),
      if (speaker.allowsDataSharing && _has(speaker.xUrl))
        _SocialLink(Icons.alternate_email, speaker.xUrl!),
    ];

    return ListView(
      padding: const EdgeInsets.all(SimfTokens.space4),
      children: <Widget>[
        _ProfileHeader(speaker: speaker, country: country, isArabic: isArabic),
        if (speaker.allowsMeetingRequests) ...<Widget>[
          const SizedBox(height: SimfTokens.space4),
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
        for (final section in sections) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(section.title),
          const SizedBox(height: SimfTokens.space2),
          Text(section.body!),
        ],
        if (speaker.sessions.isNotEmpty) ...<Widget>[
          const SizedBox(height: SimfTokens.space5),
          _SectionHeading(l10n.speakerSessionsHeading),
          const SizedBox(height: SimfTokens.space2),
          for (final session in speaker.sessions)
            _SessionRow(session: session, isArabic: isArabic),
        ],
      ],
    );
  }
}

bool _has(String? value) => value != null && value.trim().isNotEmpty;

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
    return Row(
      children: <Widget>[
        CircleAvatar(
          radius: 34,
          backgroundColor: SimfTokens.field,
          child: Text(
            speakerInitials(speaker.localizedName(isArabic)),
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: SimfTokens.textLg,
            ),
          ),
        ),
        const SizedBox(width: SimfTokens.space4),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                speaker.localizedName(isArabic),
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: SimfTokens.textXl,
                ),
              ),
              if (sub.isNotEmpty) ...<Widget>[
                const SizedBox(height: SimfTokens.space1),
                Text(
                  sub.join(' · '),
                  style: const TextStyle(color: SimfTokens.inkMuted),
                ),
              ],
            ],
          ),
        ),
      ],
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
        title: Text(session.localizedTitle(isArabic)),
        subtitle: hall == null ? null : Text(hall),
        trailing: const Icon(Icons.chevron_right, color: SimfTokens.accent),
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
        fontWeight: FontWeight.w700,
        fontSize: SimfTokens.textLg,
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
          Icon(icon, size: 56, color: SimfTokens.inkMuted),
          const SizedBox(height: SimfTokens.space3),
          Text(text, style: const TextStyle(color: SimfTokens.inkMuted)),
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
