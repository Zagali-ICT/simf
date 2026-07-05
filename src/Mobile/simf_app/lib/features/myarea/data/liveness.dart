import 'dart:typed_data';

/// The selfie the liveness flow returns to the My-Area caller, which uploads it
/// via the existing `POST /app/account/avatar`.
typedef CapturedSelfie = ({Uint8List bytes, String filename});

/// One step of the guided liveness flow (frames 758:4180 / 758:4248 / 758:4316).
enum LivenessStep { smile, turnRight, turnLeft }

/// The smile threshold (ML Kit `smilingProbability`, 0..1) that satisfies the
/// smile step.
const double kSmileProbability = 0.7;

/// The minimum absolute head yaw (ML Kit `headEulerAngleY`, degrees) that
/// satisfies a head-turn step.
const double kTurnYawDegrees = 20;

/// Pure step gate — true when the detected face satisfies [step]. Kept a plain
/// top-level function so the liveness logic is unit-testable without a camera or
/// the native plugin.
///
/// Note on sign: ML Kit's `headEulerAngleY` is positive when the head turns to
/// the image's right. The two turn steps require a yaw beyond ±[kTurnYawDegrees]
/// in opposite directions; front-camera mirroring can swap which way the user
/// perceives as "right", but a turn in each direction is still required.
bool livenessStepSatisfied(
  LivenessStep step, {
  double? smilingProbability,
  double? headEulerAngleY,
}) {
  switch (step) {
    case LivenessStep.smile:
      return smilingProbability != null &&
          smilingProbability >= kSmileProbability;
    case LivenessStep.turnRight:
      return headEulerAngleY != null && headEulerAngleY >= kTurnYawDegrees;
    case LivenessStep.turnLeft:
      return headEulerAngleY != null && headEulerAngleY <= -kTurnYawDegrees;
  }
}
