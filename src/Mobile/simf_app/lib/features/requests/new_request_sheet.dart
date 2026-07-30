import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

import '../../app/localization/app_l10n.dart';
import '../../app/theme/tokens.dart';
import 'data/request_models.dart';
import 'data/requests_repository.dart';

/// D-500 (Wave 5, الطلبات) — the "طلب جديد" flow: a bottom sheet that picks one
/// of the two app-submittable request types (participation-document / badge
/// update) and submits it. Returns `true` when a request was submitted so the
/// caller can refresh the feed.
Future<bool> showNewRequestSheet(BuildContext context) async {
  final result = await showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    backgroundColor: SimfTokens.navyDeep,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(SimfTokens.radiusLg)),
    ),
    builder: (_) => const _NewRequestSheet(),
  );
  return result ?? false;
}

enum _Step { pick, document, badge }

class _NewRequestSheet extends ConsumerStatefulWidget {
  const _NewRequestSheet();

  @override
  ConsumerState<_NewRequestSheet> createState() => _NewRequestSheetState();
}

class _NewRequestSheetState extends ConsumerState<_NewRequestSheet> {
  _Step _step = _Step.pick;
  bool _busy = false;
  String? _error;

  ParticipationDocumentType _docType =
      ParticipationDocumentType.attendanceCertificate;
  final TextEditingController _noteController = TextEditingController();
  final TextEditingController _jobTitleController = TextEditingController();
  final TextEditingController _reasonController = TextEditingController();

  @override
  void dispose() {
    _noteController.dispose();
    _jobTitleController.dispose();
    _reasonController.dispose();
    super.dispose();
  }

  Future<void> _submitDocument(AppL10n l10n) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(requestsRepositoryProvider).submitDocumentRequest(
            documentType: _docType,
            note: _noteController.text,
          );
      if (mounted) {
        Navigator.of(context).pop(true);
      }
    } on ApiFailure {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = l10n.requestSubmitFailed;
        });
      }
    }
  }

  Future<void> _submitBadge(AppL10n l10n) async {
    if (_jobTitleController.text.trim().isEmpty) {
      setState(() => _error = l10n.requestBadgeTitleRequired);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(requestsRepositoryProvider).submitBadgeRequest(
            requestedJobTitle: _jobTitleController.text,
            note: _reasonController.text,
          );
      if (mounted) {
        Navigator.of(context).pop(true);
      }
    } on ApiFailure {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = l10n.requestSubmitFailed;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppL10n.of(context);
    final bottomInset = MediaQuery.of(context).viewInsets.bottom;
    return Padding(
      padding: EdgeInsets.only(bottom: bottomInset),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.all(SimfTokens.space4),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  decoration: BoxDecoration(
                    color: SimfTokens.line,
                    borderRadius: BorderRadius.circular(SimfTokens.radiusSmall),
                  ),
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              Text(
                _step == _Step.pick ? l10n.requestNewTitle : l10n.requestNew,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textLg,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: SimfTokens.space4),
              if (_error != null) ...<Widget>[
                Text(
                  _error!,
                  style: const TextStyle(color: SimfTokens.danger),
                ),
                const SizedBox(height: SimfTokens.space3),
              ],
              ..._buildStep(l10n),
            ],
          ),
        ),
      ),
    );
  }

  List<Widget> _buildStep(AppL10n l10n) {
    switch (_step) {
      case _Step.pick:
        return <Widget>[
          _PickTile(
            icon: Icons.description_outlined,
            label: l10n.requestNewDocument,
            onTap: () => setState(() => _step = _Step.document),
          ),
          const SizedBox(height: SimfTokens.space3),
          _PickTile(
            icon: Icons.badge_outlined,
            label: l10n.requestNewBadge,
            onTap: () => setState(() => _step = _Step.badge),
          ),
        ];
      case _Step.document:
        return <Widget>[
          Text(
            l10n.requestDocTypeLabel,
            style: const TextStyle(
              color: SimfTokens.beigeBorder,
              fontSize: SimfTokens.textSm,
            ),
          ),
          const SizedBox(height: SimfTokens.space2),
          for (final type in ParticipationDocumentType.values)
            _RadioRow(
              label: _docTypeLabel(l10n, type),
              selected: _docType == type,
              onTap: () => setState(() => _docType = type),
            ),
          const SizedBox(height: SimfTokens.space3),
          _SheetField(
            controller: _noteController,
            label: l10n.requestNoteLabel,
            maxLines: 3,
          ),
          const SizedBox(height: SimfTokens.space4),
          _SubmitButton(
            label: l10n.requestSubmit,
            busy: _busy,
            onTap: () => unawaited(_submitDocument(l10n)),
          ),
        ];
      case _Step.badge:
        return <Widget>[
          _SheetField(
            controller: _jobTitleController,
            label: l10n.requestBadgeTitleLabel,
          ),
          const SizedBox(height: SimfTokens.space3),
          _SheetField(
            controller: _reasonController,
            label: l10n.requestBadgeReasonLabel,
            maxLines: 3,
          ),
          const SizedBox(height: SimfTokens.space4),
          _SubmitButton(
            label: l10n.requestSubmit,
            busy: _busy,
            onTap: () => unawaited(_submitBadge(l10n)),
          ),
        ];
    }
  }

  String _docTypeLabel(AppL10n l10n, ParticipationDocumentType type) {
    switch (type) {
      case ParticipationDocumentType.participationLetter:
        return l10n.requestDocTypeParticipation;
      case ParticipationDocumentType.invitationLetter:
        return l10n.requestDocTypeInvitation;
      case ParticipationDocumentType.attendanceCertificate:
        return l10n.requestDocTypeAttendance;
    }
  }
}

class _PickTile extends StatelessWidget {
  const _PickTile({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(SimfTokens.radius),
      child: Container(
        padding: const EdgeInsets.all(SimfTokens.space3),
        decoration: BoxDecoration(
          color: SimfTokens.navy,
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          border: Border.all(color: SimfTokens.accent, width: SimfTokens.hairline),
        ),
        child: Row(
          children: <Widget>[
            Icon(icon, color: SimfTokens.accent, size: 22),
            const SizedBox(width: SimfTokens.space3),
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textMd,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
            const Icon(Icons.chevron_left, color: SimfTokens.beigeBorder),
          ],
        ),
      ),
    );
  }
}

class _RadioRow extends StatelessWidget {
  const _RadioRow({
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
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: SimfTokens.space2),
        child: Row(
          children: <Widget>[
            Icon(
              selected ? Icons.radio_button_checked : Icons.radio_button_off,
              color: selected ? SimfTokens.accent : SimfTokens.beigeBorder,
              size: 20,
            ),
            const SizedBox(width: SimfTokens.space2),
            Expanded(
              child: Text(
                label,
                style: const TextStyle(
                  color: SimfTokens.surface,
                  fontSize: SimfTokens.textMd,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _SheetField extends StatelessWidget {
  const _SheetField({
    required this.controller,
    required this.label,
    this.maxLines = 1,
  });

  final TextEditingController controller;
  final String label;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      maxLines: maxLines,
      style: const TextStyle(color: SimfTokens.surface),
      decoration: InputDecoration(
        labelText: label,
        labelStyle: const TextStyle(color: SimfTokens.beigeBorder),
        filled: true,
        fillColor: SimfTokens.navy,
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.line),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(SimfTokens.radius),
          borderSide: const BorderSide(color: SimfTokens.accent),
        ),
      ),
    );
  }
}

class _SubmitButton extends StatelessWidget {
  const _SubmitButton({
    required this.label,
    required this.busy,
    required this.onTap,
  });

  final String label;
  final bool busy;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: ElevatedButton(
        onPressed: busy ? null : onTap,
        style: ElevatedButton.styleFrom(
          backgroundColor: SimfTokens.accent,
          foregroundColor: SimfTokens.navy,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(SimfTokens.radius),
          ),
        ),
        child: busy
            ? const SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: SimfTokens.navy,
                ),
              )
            : Text(
                label,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
      ),
    );
  }
}
