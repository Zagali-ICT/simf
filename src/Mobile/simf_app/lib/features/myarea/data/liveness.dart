import 'dart:typed_data';

import 'package:flutter/foundation.dart' show TargetPlatform;

/// The selfie the liveness flow returns to the My-Area caller, which uploads it
/// via the existing `POST /app/account/avatar`.
typedef CapturedSelfie = ({Uint8List bytes, String filename});

/// One step of the guided liveness flow (frames 758:4180 / 758:4248 / 758:4316).
enum LivenessStep { smile, turnRight, turnLeft }

/// The physical direction a turn step asks the user to face. Kept a pure
/// function of the step (NO platform branch) so the prompt and the arrow always
/// match the step name on both platforms — the per-platform yaw-sign difference
/// is handled only in [livenessStepSatisfied] via `invertYaw`. Extracting it
/// pins the invariant that the D-684 / PR-103 prompt swap is not re-introduced.
enum LivenessPromptDirection { none, right, left }

/// Maps a [step] to the physical direction its prompt + arrow must show.
LivenessPromptDirection livenessPromptDirection(LivenessStep step) =>
    switch (step) {
      LivenessStep.smile => LivenessPromptDirection.none,
      LivenessStep.turnRight => LivenessPromptDirection.right,
      LivenessStep.turnLeft => LivenessPromptDirection.left,
    };

/// Whether ML Kit's `headEulerAngleY` must be sign-inverted for [platform]
/// before the turn gate. iOS mirrors the front-camera image and is fed a
/// different InputImage rotation, so the same physical turn arrives with the
/// opposite sign; inverting on iOS makes a positive yaw a physical RIGHT turn
/// on every platform. A pure function so the per-platform decision is
/// unit-tested (the inline `Platform.isIOS` at the call site was untestable).
bool livenessInvertYaw(TargetPlatform platform) =>
    platform == TargetPlatform.iOS;

/// The smile threshold (ML Kit `smilingProbability`, 0..1) that satisfies the
/// smile step.
const double kSmileProbability = 0.7;

/// The minimum absolute head yaw (ML Kit `headEulerAngleY`, degrees) that
/// satisfies a head-turn step.
const double kTurnYawDegrees = 20;

/// Pure step gate — true when the detected face satisfies [step]. Kept a plain
/// top-level function so the liveness logic is unit-testable without a camera
/// or the native plugin.
///
/// **Yaw sign convention.** ML Kit `headEulerAngleY` reaches this gate with the
/// OPPOSITE sign on iOS vs Android for the same physical head turn: the front
/// camera is mirrored and `identity_verification_screen` feeds ML Kit a
/// different input-image rotation per platform (raw sensor on iOS, device-
/// orientation-compensated on Android). Earlier fixes (D-684, PR-103) tried to
/// compensate in the prompt/arrow, which mislabels the step and left iOS
/// turning the wrong way. Instead we normalise the sign HERE: the caller passes
/// [invertYaw] = true on iOS so that, after normalisation, a **positive yaw is
/// always a physical RIGHT turn** on every platform. The prompt and arrow can
/// then always match the step name (turnRight → "turn right" + right arrow).
bool livenessStepSatisfied(
  LivenessStep step, {
  double? smilingProbability,
  double? headEulerAngleY,
  bool invertYaw = false,
}) {
  switch (step) {
    case LivenessStep.smile:
      return smilingProbability != null &&
          smilingProbability >= kSmileProbability;
    case LivenessStep.turnRight:
    case LivenessStep.turnLeft:
      if (headEulerAngleY == null) {
        return false;
      }
      // Normalise so a positive yaw is a physical RIGHT turn on every platform.
      final yaw = invertYaw ? -headEulerAngleY : headEulerAngleY;
      return step == LivenessStep.turnRight
          ? yaw >= kTurnYawDegrees
          : yaw <= -kTurnYawDegrees;
  }
}
