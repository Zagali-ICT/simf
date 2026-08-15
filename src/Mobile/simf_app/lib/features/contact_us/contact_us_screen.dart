import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/app/widgets/simf_page_shell.dart';
import 'package:simf_app/core/organization_profile/organization_profile.dart';
import 'package:simf_app/features/contact_us/data/contact_us_repository.dart';
import 'package:simf_app/features/contact_us/widgets/contact_info_card.dart';
import 'package:simf_app/features/contact_us/widgets/contact_send_message_card.dart';
import 'package:simf_app/features/contact_us/widgets/contact_social_card.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// Page 203 — تواصل معنا · Contact us (`/contact-us`, public). Pixel-parity to
/// KSA Figma frame **1388:7711**: the navy [SimfPageShell] shell over a "أرسل
/// رسالة" form (name / email / message + send), a "معلومات التواصل" panel
/// (phone / email / location from the shared org profile) and the social-links
/// row. Previously a ComingSoon placeholder (D-464).
///
/// The form posts to the new public `POST /app/contact-inquiry`; the info panel
/// and social links read the app-lifetime [orgProfileProvider] (same data the
/// About screen uses), so only the fields the admin actually set are shown.
///
/// Route: `RouteNames.contactUs`.
/// Data: [contactUsRepositoryProvider], [orgProfileProvider].
/// Perf: ListView builds every child up front — correct for a short static
///       page, a defect on a data feed.
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

    return SimfPageShell(
      title: l10n.contactUsTitle,
      onBack: () => backOrHome(context),
      body: SimfPullToRefresh(
        onRefresh: () => ref.read(orgProfileProvider.notifier).warm(),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space4,
            SimfTokens.space6,
          ),
          children: <Widget>[
            ContactSendMessageCard(
              formKey: _formKey,
              name: _name,
              email: _email,
              message: _message,
              sending: _sending,
              onSend: () => unawaited(_send()),
            ),
            if (profile != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space6), // gap-24
              ContactInfoCard(profile: profile, isArabic: isArabic),
              if (_hasAnySocial(profile.social)) ...<Widget>[
                const SizedBox(height: SimfTokens.space6), // gap-24
                ContactSocialCard(social: profile.social),
              ],
            ],
          ],
        ),
      ),
    );
  }

  static bool _hasAnySocial(OrgSocial s) =>
      <String?>[s.x, s.instagram, s.linkedin, s.youtube, s.tiktok]
          .any((u) => u != null && u.trim().isNotEmpty);
}
