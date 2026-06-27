import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import '../../app/widgets/ksa_shell.dart';
import '../../core/external_link.dart';
import '../../core/organization_profile/organization_profile.dart';
import 'data/contact_us_repository.dart';

/// Page 203 — تواصل معنا · Contact us (`/contact-us`, public). Pixel-parity to
/// KSA Figma frame **1388:7711**: the navy [KsaPage] shell over a "أرسل رسالة"
/// form (name / email / message + send), a "معلومات التواصل" panel (phone /
/// email / location from the shared org profile) and the social-links row.
/// Previously a ComingSoon placeholder (D-464).
///
/// The form posts to the new public `POST /app/contact-inquiry`; the info panel
/// and social links read the app-lifetime [orgProfileProvider] (same data the
/// About screen uses), so only the fields the admin actually set are shown.
class ContactUsScreen extends ConsumerStatefulWidget {
  const ContactUsScreen({super.key});

  @override
  ConsumerState<ContactUsScreen> createState() => _ContactUsScreenState();
}

class _ContactUsScreenState extends ConsumerState<ContactUsScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _message = TextEditingController();
  bool _sending = false;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _message.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final l10n = AppL10n.of(context);
    final messenger = ScaffoldMessenger.of(context);
    if (!_formKey.currentState!.validate()) {
      return;
    }
    setState(() => _sending = true);
    try {
      await ref.read(contactUsRepositoryProvider).submit(
            name: _name.text.trim(),
            email: _email.text.trim(),
            message: _message.text.trim(),
          );
      if (!mounted) {
        return;
      }
      _name.clear();
      _email.clear();
      _message.clear();
      _formKey.currentState!.reset();
      messenger.showSnackBar(SnackBar(content: Text(l10n.contactSentToast)));
    } on ApiFailure {
      if (!mounted) {
        return;
      }
      messenger.showSnackBar(SnackBar(content: Text(l10n.contactSendFailed)));
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final isArabic = l10n.isArabic;
    final profile = ref.watch(orgProfileProvider);

    return KsaPage(
      title: l10n.contactUsTitle,
      onBack: () => ksaBackOrHome(context),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space4,
          SimfTokens.space6,
        ),
        children: <Widget>[
          _SendMessageCard(
            formKey: _formKey,
            name: _name,
            email: _email,
            message: _message,
            sending: _sending,
            onSend: () => unawaited(_send()),
          ),
          if (profile != null) ...<Widget>[
            const SizedBox(height: SimfTokens.space6), // gap-24
            _ContactInfoCard(profile: profile, isArabic: isArabic),
            if (_hasAnySocial(profile.social)) ...<Widget>[
              const SizedBox(height: SimfTokens.space6), // gap-24
              _SocialCard(social: profile.social),
            ],
          ],
        ],
      ),
    );
  }

  static bool _hasAnySocial(OrgSocial s) =>
      <String?>[s.x, s.instagram, s.linkedin, s.youtube, s.tiktok]
          .any((u) => u != null && u.trim().isNotEmpty);
}

/// The "أرسل رسالة" form card (frame node 1388:7711): name / email / message
/// fields over the gold send button, on the navy-deep card chrome.
class _SendMessageCard extends StatelessWidget {
  const _SendMessageCard({
    required this.formKey,
    required this.name,
    required this.email,
    required this.message,
    required this.sending,
    required this.onSend,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController name;
  final TextEditingController email;
  final TextEditingController message;
  final bool sending;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    return _Card(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            _CardHeading(l10n.contactSendTitle),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactNameLabel,
              hint: l10n.contactNameHint,
              controller: name,
              textInputAction: TextInputAction.next,
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? l10n.contactNameRequired
                  : null,
            ),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactEmailLabel,
              hint: l10n.contactEmailHint,
              controller: email,
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              validator: (v) {
                final value = (v ?? '').trim();
                if (value.isEmpty || !value.contains('@') ||
                    !value.contains('.')) {
                  return l10n.contactEmailInvalid;
                }
                return null;
              },
            ),
            const SizedBox(height: SimfTokens.space4),
            _Field(
              label: l10n.contactMessageLabel,
              hint: l10n.contactMessageHint,
              controller: message,
              maxLines: 5,
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? l10n.contactMessageRequired
                  : null,
            ),
            const SizedBox(height: SimfTokens.space5),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: sending ? null : onSend,
                child: sending
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(l10n.contactSendButton),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One labelled text field (navy fill, beige hairline, white text).
class _Field extends StatelessWidget {
  const _Field({
    required this.label,
    required this.hint,
    required this.controller,
    this.validator,
    this.keyboardType,
    this.textInputAction,
    this.maxLines = 1,
  });

  final String label;
  final String hint;
  final TextEditingController controller;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: const TextStyle(
            color: SimfTokens.beigeBorder, // Figma 1388:7778 — beige label
            fontSize: SimfTokens.textSm, // 12
            fontWeight: FontWeight.w500,
          ),
        ),
        const SizedBox(height: SimfTokens.space2),
        TextFormField(
          controller: controller,
          validator: validator,
          keyboardType: keyboardType,
          textInputAction: textInputAction,
          maxLines: maxLines,
          style: const TextStyle(color: Colors.white, fontSize: SimfTokens.textMd),
          decoration: InputDecoration(
            hintText: hint,
            hintStyle: const TextStyle(color: SimfTokens.beigeBorder),
            filled: true,
            // Same fill as the card (border-only field) — Figma 1388:7779.
            fillColor: SimfTokens.navyDeep,
            contentPadding: const EdgeInsets.symmetric(
              horizontal: SimfTokens.space3,
              vertical: SimfTokens.space3,
            ),
            enabledBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.beigeBorder),
            ),
            focusedBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.accent),
            ),
            border: OutlineInputBorder(
              borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
              borderSide: const BorderSide(color: SimfTokens.beigeBorder),
            ),
          ),
        ),
      ],
    );
  }
}

/// The "معلومات التواصل" panel (frame node 1388:7711): phone / email / location
/// rows, each with a gold icon, from the shared org profile.
class _ContactInfoCard extends StatelessWidget {
  const _ContactInfoCard({required this.profile, required this.isArabic});

  final OrgProfile profile;
  final bool isArabic;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final rows = <(IconData, String, String, bool)>[
      if (profile.contactPhone != null && profile.contactPhone!.isNotEmpty)
        (Icons.call, profile.contactPhone!, l10n.contactHotlineLabel, true),
      if (profile.contactEmail != null && profile.contactEmail!.isNotEmpty)
        (Icons.mail_outline, profile.contactEmail!, l10n.contactEmailLabel, true),
      if ((profile.locationFor(isArabic) ?? '').isNotEmpty)
        (Icons.location_on_outlined, profile.locationFor(isArabic)!,
            l10n.contactLocationLabel, false),
    ];
    if (rows.isEmpty) {
      return const SizedBox.shrink();
    }
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(l10n.contactInfoTitle),
          const SizedBox(height: SimfTokens.space4), // gap-16
          for (final (icon, value, sub, ltr) in rows)
            _InfoRow(icon: icon, value: value, sublabel: sub, valueLtr: ltr),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.value,
    required this.sublabel,
    required this.valueLtr,
  });

  final IconData icon;
  final String value;
  final String sublabel;
  final bool valueLtr;

  @override
  Widget build(BuildContext context) {
    return Container(
      // The frame walls each info row with a faint beige bottom divider
      // (Figma 1388:7723 — beige @25%).
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(
            color: SimfTokens.beigeBorder.withValues(alpha: 0.25),
            width: SimfTokens.hairline,
          ),
        ),
      ),
      padding: const EdgeInsets.all(SimfTokens.space2), // p-8
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  value,
                  textAlign: TextAlign.start,
                  textDirection: valueLtr ? TextDirection.ltr : null,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: SimfTokens.textMd, // 14
                    fontWeight: FontWeight.w500,
                  ),
                ),
                const SizedBox(height: SimfTokens.space2), // gap-8
                Text(
                  sublabel,
                  style: const TextStyle(
                    color: SimfTokens.beigeBorder,
                    fontSize: SimfTokens.textSm, // 12
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
            decoration: const BoxDecoration(
              color: SimfTokens.accent,
              borderRadius: BorderRadius.all(Radius.circular(SimfTokens.radius)),
            ),
            child: Icon(icon, color: SimfTokens.navy, size: 18),
          ),
        ],
      ),
    );
  }
}

/// The "وسائل التواصل الاجتماعي" row (frame node 1388:7711): one bordered tap
/// box per set social link. Brand-accurate glyphs are pending a Figma asset
/// export — Material approximations are used until then.
class _SocialCard extends StatelessWidget {
  const _SocialCard({required this.social});

  final OrgSocial social;

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final links = <(IconData, String, String?)>[
      (Icons.close, 'X', social.x),
      (Icons.camera_alt_outlined, 'Instagram', social.instagram),
      (Icons.business_center_outlined, 'LinkedIn', social.linkedin),
      (Icons.smart_display_outlined, 'YouTube', social.youtube),
      (Icons.music_note_outlined, 'TikTok', social.tiktok),
    ].where((l) => l.$3 != null && l.$3!.trim().isNotEmpty).toList();
    if (links.isEmpty) {
      return const SizedBox.shrink();
    }
    return _Card(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _CardHeading(l10n.contactSocialTitle),
          const SizedBox(height: SimfTokens.space4), // gap-16
          // The frame lays the brand boxes left→right (X … TikTok); force LTR so
          // they keep that order under RTL. Spread edge-to-edge only when the
          // full five are set (the frame's layout); fewer links cluster at the
          // start so a partial set never floats to opposite edges. (At most five
          // social fields exist, so 5×48 always fits.)
          Directionality(
            textDirection: TextDirection.ltr,
            child: Row(
              mainAxisAlignment: links.length >= 5
                  ? MainAxisAlignment.spaceBetween
                  : MainAxisAlignment.start,
              children: <Widget>[
                for (var i = 0; i < links.length; i++) ...<Widget>[
                  if (links.length < 5 && i > 0)
                    const SizedBox(width: SimfTokens.space3),
                  _SocialButton(
                    icon: links[i].$1,
                    label: links[i].$2,
                    onTap: () =>
                        unawaited(launchExternalUri(Uri.parse(links[i].$3!))),
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

class _SocialButton extends StatelessWidget {
  const _SocialButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
        child: Container(
          width: 48,
          height: 48,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: SimfTokens.navy,
            borderRadius: BorderRadius.circular(SimfTokens.radius), // 8
            border: Border.all(
              color: SimfTokens.beigeBorder,
              width: SimfTokens.hairline,
            ),
          ),
          child: Icon(icon, color: Colors.white, size: 20),
        ),
      ),
    );
  }
}

/// Shared navy-deep card chrome for the contact sections.
class _Card extends StatelessWidget {
  const _Card({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      // px-16 / py-8 (Figma card 1388:7774).
      padding: const EdgeInsets.symmetric(
        horizontal: SimfTokens.space4,
        vertical: SimfTokens.space2,
      ),
      decoration: BoxDecoration(
        color: SimfTokens.navyDeep,
        borderRadius: BorderRadius.circular(SimfTokens.radius),
      ),
      child: child,
    );
  }
}

class _CardHeading extends StatelessWidget {
  const _CardHeading(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      textAlign: TextAlign.start,
      style: const TextStyle(
        color: Colors.white,
        fontSize: SimfTokens.textLg, // 16
        fontWeight: FontWeight.w500, // Medium (Figma 1388:7775)
      ),
    );
  }
}
