import 'dart:async';

import 'package:flutter/material.dart';
import 'package:simf_app/app/localization/app_l10n.dart';
import 'package:simf_app/app/theme/tokens.dart';
import 'package:simf_app/features/speakers/meeting_request_validation.dart';
import 'package:simf_app/features/speakers/widgets/meeting_send_button.dart';
import 'package:simf_app/features/speakers/widgets/meeting_sheet_fields.dart';
import 'package:simf_app/features/speakers/widgets/meeting_slot_section.dart';
import 'package:simf_app/features/speakers/widgets/meeting_target_picker.dart';
import 'package:simf_data_pkg/simf_data_pkg.dart';

/// The beige meeting-request bottom sheet, Figma **1776:5036**: a gold drag
/// handle, the title, the الموضوع subject field, the target's available days
/// (اختيار التاريخ) with that day's time slots (اختيار الوقت), then the gold
/// "ارسال الطلب" button.
///
/// One form, two sheets. `MeetingRequestSheet` (a speaker, 1776:5036) and
/// `DelegationMeetingRequestSheet` (an invited delegation) were 79% the same
/// file; everything that genuinely differs between them — the target type, its
/// repository calls, the copy, the widget-key namespace, and the delegation's
/// extra عدد الحضور field — arrives as configuration. The two sheets stay as
/// the public types their screens, tests and goldens name.
///
/// Each has two entry points:
/// - opened from a profile / a tapped card — [target] is set, so it is fixed
///   and no picker is shown;
/// - opened from the "طلب جديد" on the requests list (اللقاءات الثنائية,
///   1408:9726) — [target] is **null**, so the sheet first shows a searchable
///   picker (type-to-filter, owner 2026-07-11).
///
/// D-709 (item 6, FDS-013 §15.4 GAP-4) — the date + time come from the
/// target's **real availability slots**, NOT a free client-side grid; this
/// **reverts D-703**. Eligibility is enforced server-side on the per-user
/// `allowsSpeakerMeeting` / `allowsDelegationMeeting` flag (D-760, replacing
/// the VIP-tier gate).
///
/// G3 (owner 2026-07-30, supersedes D-767 R1) — with **no free slot** the
/// request can no longer be sent subject-only: the sheet shows the "no slots"
/// notice and the send button is disabled (the API 409s
/// `*_MEETING_NO_AVAILABILITY`). A **failed** slot fetch is a separate state —
/// it shows a load error + Retry, so a transient network failure never
/// masquerades as "this target has no time".
class MeetingRequestForm<T> extends StatefulWidget {
  const MeetingRequestForm({
    required this.target,
    required this.targetId,
    required this.l10n,
    required this.title,
    required this.keyPrefix,
    required this.searchFieldKey,
    required this.pickerLabel,
    required this.pickerEmptyHint,
    required this.pinSelectedInPicker,
    required this.noTargetSelectedError,
    required this.loadTargets,
    required this.loadSlots,
    required this.submit,
    required this.failureText,
    this.extraFields = const <Widget>[],
    this.validateExtra,
    super.key,
  });

  /// The fixed target, or **null** for the bilateral entry (show the picker).
  final T? target;
  final String Function(T target) targetId;
  final AppL10n l10n;

  /// طلب مقابلة / طلب اجتماع وفد.
  final String title;

  /// `meeting` or `delegation` — the widget-key namespace the sheet tests and
  /// the E2E catalogue drive the subject / day / time / retry controls by.
  final String keyPrefix;

  /// Irregular enough to pass whole: the speaker sheet's search field is keyed
  /// `meeting-speaker-search`, the delegation's `delegation-search`.
  final Key searchFieldKey;
  final String pickerLabel;
  final String pickerEmptyHint;
  final bool pinSelectedInPicker;
  final String noTargetSelectedError;
  final Future<List<MeetingTargetOption<T>>> Function() loadTargets;
  final Future<List<MeetingSlot>> Function(T target) loadSlots;
  final Future<void> Function({
    required T target,
    required String subject,
    required DateTime slotStart,
    required DateTime slotEnd,
  }) submit;

  /// Maps a failed submit onto the inline error. The two sheets disagree about
  /// what a 403 and a 409 mean, so neither mapping can be shared.
  final String Function(ApiFailure failure) failureText;

  /// Rendered above the subject field — the delegation sheet's عدد الحضور
  /// input and its label.
  final List<Widget> extraFields;

  /// Validates [extraFields], between the subject check and the slot check.
  final String? Function()? validateExtra;

  @override
  State<MeetingRequestForm<T>> createState() => _MeetingRequestFormState<T>();
}

class _MeetingRequestFormState<T> extends State<MeetingRequestForm<T>> {
  final TextEditingController _subject = TextEditingController();
  bool _submitting = false;
  // R0 (D-767) — inline validation / API-failure feedback. A ScaffoldMessenger
  // snackbar shown while this modal sheet is open renders behind it
  // (invisible).
  String? _error;
  // The target the request goes to: the fixed [widget.target], or the one
  // picked from the list in the bilateral flow.
  T? _target;
  // The picker's candidates (bilateral flow only; empty on the fixed flow).
  List<MeetingTargetOption<T>> _targets = <MeetingTargetOption<T>>[];
  bool _targetsLoaded = false;
  String _query = '';
  // The chosen target's real availability slots (D-709), loaded once a target
  // is set. Empty (once loaded) means the "no slots" state.
  List<MeetingSlot> _slots = const <MeetingSlot>[];
  bool _slotsLoading = false;
  bool _slotsError = false;
  DateTime? _selectedDay;
  MeetingSlot? _selectedSlot;

  @override
  void initState() {
    super.initState();
    _target = widget.target;
    final fixed = _target;
    if (fixed == null) {
      unawaited(_loadTargets());
    } else {
      unawaited(_loadSlots(fixed));
    }
  }

  @override
  void dispose() {
    _subject.dispose();
    super.dispose();
  }

  String? get _selectedId {
    final target = _target;
    return target == null ? null : widget.targetId(target);
  }

  Future<void> _loadTargets() async {
    try {
      final targets = await widget.loadTargets();
      if (!mounted) {
        return;
      }
      setState(() {
        _targets = targets;
        _targetsLoaded = true;
      });
    } on Object {
      // Wider than ApiFailure: a keystore error on the 401-refresh escapes
      // un-wrapped. initState is the only caller, so an unloaded picker would
      // spin for the life of the sheet with no retry path.
      if (!mounted) {
        return;
      }
      setState(() => _targetsLoaded = true);
    }
  }

  /// Load the chosen target's real availability slots (D-709). G3 — a failure
  /// is NO LONGER folded into "no slots": now that an empty list disables
  /// sending, a swallowed network error would tell the user the target has no
  /// availability and leave them stuck, so the failure gets its own state + a
  /// Retry.
  Future<void> _loadSlots(T target) async {
    final id = widget.targetId(target);
    setState(() {
      _slotsLoading = true;
      _slotsError = false;
      _slots = const <MeetingSlot>[];
      _selectedDay = null;
      _selectedSlot = null;
    });
    try {
      final slots = await widget.loadSlots(target);
      // Drop a stale response — in the bilateral flow the user may have
      // switched to another target while this load was in flight.
      if (!mounted || id != _selectedId) {
        return;
      }
      setState(() {
        _slots = slots;
        _slotsLoading = false;
      });
    } on Object {
      // Wider than ApiFailure: a keystore error on the 401-refresh escapes
      // un-wrapped. Every failure reaches the G3 load-error state, never the
      // "no availability" notice, since the Retry hangs off _slotsError.
      if (!mounted || id != _selectedId) {
        return;
      }
      setState(() {
        _slotsLoading = false;
        _slotsError = true;
      });
    }
  }

  void _onTargetSelected(MeetingTargetOption<T> option) {
    if (option.id == _selectedId) {
      return;
    }
    setState(() => _target = option.value);
    unawaited(_loadSlots(option.value));
  }

  void _retrySlots() {
    final target = _target;
    if (target == null) {
      return;
    }
    unawaited(_loadSlots(target));
  }

  Future<void> _submit() async {
    final l10n = widget.l10n;
    final target = _target;
    final subject = _subject.text.trim();
    final slot = _selectedSlot;
    final invalid = meetingRequestError(
      l10n: l10n,
      hasTarget: target != null,
      noTargetSelectedError: widget.noTargetSelectedError,
      subject: subject,
      validateExtra: widget.validateExtra,
      hasSlot: slot != null,
    );
    if (invalid != null) {
      setState(() => _error = invalid);
      return;
    }
    // R0 — clear the inline error and submit. Feedback stays inside the sheet.
    setState(() {
      _submitting = true;
      _error = null;
    });
    final navigator = Navigator.of(context);
    final messenger = ScaffoldMessenger.of(context);
    var popped = false;
    try {
      await widget.submit(
        // `as T`, not `!` — T itself may be a nullable type.
        target: target as T,
        subject: subject,
        slotStart: slot!.start,
        slotEnd: slot.end,
      );
      if (!mounted) {
        return;
      }
      // Success pops the sheet first, so this toast is visible (not occluded).
      navigator.pop();
      popped = true;
      messenger.showSnackBar(SnackBar(content: Text(l10n.meetingRequestSent)));
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() => _error = widget.failureText(failure));
    } on Object {
      // Wider than ApiFailure, as in _loadSlots: a keystore error on the
      // 401-refresh escapes un-wrapped and would reach the zone unreported.
      if (!mounted) {
        return;
      }
      setState(() => _error = l10n.meetingRequestFailed);
    } finally {
      // Only for a sheet that is STAYING: `mounted` stays true through the
      // ~200ms exit after pop(), so re-enabling would repaint a leaving sheet.
      if (!popped && mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = widget.l10n;
    final canSend = !_submitting && !_slotsLoading && _slots.isNotEmpty;
    return SingleChildScrollView(
      padding: EdgeInsets.fromLTRB(
        SimfTokens.space4,
        SimfTokens.space3,
        SimfTokens.space4,
        MediaQuery.of(context).viewInsets.bottom + SimfTokens.space6,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Center(
            child: Container(
              width: SimfTokens.meetingRequestSheetWidthMd,
              height: SimfTokens.meetingRequestSheetHeightSm,
              decoration: BoxDecoration(
                color: SimfTokens.accent,
                borderRadius: BorderRadius.circular(SimfTokens.radiusLarge),
              ),
            ),
          ),
          const SizedBox(height: SimfTokens.space4),
          Text(
            widget.title,
            textAlign: TextAlign.end,
            style: SimfTokens.labelInkSemiboldTitle,
          ),
          const SizedBox(height: SimfTokens.space4),
          // Bilateral entry (target == null): the picker. A speaker profile or
          // a tapped delegation card fixes the target, so it shows no picker.
          if (widget.target == null) ...<Widget>[
            MeetingFieldLabel(text: widget.pickerLabel),
            const SizedBox(height: SimfTokens.space2),
            MeetingTargetPicker<T>(
              loaded: _targetsLoaded,
              options: _targets,
              selectedId: _selectedId,
              query: _query,
              searchFieldKey: widget.searchFieldKey,
              searchHint: l10n.speakersSearchHint,
              emptyHint: widget.pickerEmptyHint,
              noMatchesHint: l10n.speakersNoMatches,
              pinSelected: widget.pinSelectedInPicker,
              enabled: !_submitting,
              onQueryChanged: (value) => setState(() => _query = value),
              onSelected: _onTargetSelected,
            ),
            const SizedBox(height: SimfTokens.space4),
          ],
          // The rest appears once a target is set (always on the fixed flow;
          // after a pick on the bilateral flow).
          if (_target != null) ...<Widget>[
            ...widget.extraFields,
            MeetingFieldLabel(text: l10n.meetingSubjectLabel), // الموضوع
            const SizedBox(height: SimfTokens.space2),
            MeetingSubjectField(
              fieldKey: ValueKey<String>('${widget.keyPrefix}-subject'),
              controller: _subject,
              hintText: l10n.meetingSubjectHint, // اكتب الموضوع
            ),
            const SizedBox(height: SimfTokens.space4),
            MeetingSlotSection(
              slots: _slots,
              loading: _slotsLoading,
              loadFailed: _slotsError,
              selectedDay: _selectedDay,
              selectedSlot: _selectedSlot,
              isArabic: l10n.isArabic,
              l10n: l10n,
              keyPrefix: widget.keyPrefix,
              onRetry: _retrySlots,
              onDaySelected: (day) => setState(() {
                _selectedDay = day;
                _selectedSlot = null; // a new day, pick from its own slots
              }),
              onSlotSelected: (slot) => setState(() => _selectedSlot = slot),
            ),
            if (_error != null) ...<Widget>[
              const SizedBox(height: SimfTokens.space3),
              Align(
                alignment: AlignmentDirectional.centerStart,
                child: Text(_error!, style: SimfTokens.bodyDanger),
              ),
            ],
            const SizedBox(height: SimfTokens.space5),
            MeetingSendButton(
              label: _submitting ? l10n.loadingLabel : l10n.meetingSendButton,
              enabled: canSend,
              onTap: () => unawaited(_submit()),
            ),
          ],
        ],
      ),
    );
  }
}
