using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Eye Transform/Corrected mode virtual target updates
    /// </summary>
    public partial class LookTargetController
    {
        /// <summary>
        /// Update eye virtual target (single target for both eyes - Transform mode)
        /// </summary>
        private void UpdateEyeVirtualTarget(float deltaTime)
        {
            if (eyeVirtualTarget == null)
                return;

            // Use center position between both eyes if both exist, otherwise use whichever exists
            Vector3 eyeCenter;
            if (leftEyeBall != null && rightEyeBall != null)
            {
                eyeCenter = (leftEyeBall.position + rightEyeBall.position) * 0.5f;
            }
            else if (leftEyeBall != null)
            {
                eyeCenter = leftEyeBall.position;
            }
            else if (rightEyeBall != null)
            {
                eyeCenter = rightEyeBall.position;
            }
            else
            {
                return; // No eyes available
            }

            Vector3 directionToTarget = target.position - eyeCenter;
            float sqrDistance = directionToTarget.sqrMagnitude;

            Vector3 targetPos;
            if (sqrDistance < minDistanceSqr)
            {
                // Normalize only when needed
                targetPos = eyeCenter + directionToTarget.normalized * minDistance;
            }
            else
            {
                targetPos = target.position;
            }

            if (!eyeVtSeeded)
            {
                eyeVtSmoothed = eyeVirtualTarget.position;
                eyeVtSeeded = true;
            }

            eyeVtSmoothed = Vector3.Lerp(eyeVtSmoothed, targetPos, Mathf.Clamp01(eyeSpeed * deltaTime));
            eyeVirtualTarget.position = eyeVtSmoothed;
        }

        /// <summary>
        /// Update left/right eye virtual targets (TransformCorrected mode).
        /// Each eye gets its own virtual target placed directly at the real look target, so the two
        /// eyes converge correctly. The per-frame direction "correction" that used to live here was
        /// removed: it could not steer the gaze horizontally when the eye bone's aim axis was not its
        /// gaze axis (the horizontal component degenerated into roll about the gaze axis, so only
        /// vertical tracking worked). The aim axis is instead detected and configured once at init
        /// (FluentTAvatarControllerFloatingHead.AutoConfigureLookAimAxes), so aiming the eye's gaze axis at the
        /// real target now tracks correctly on all axes.
        /// </summary>
        private void UpdateEyeVirtualTargetsCorrected(float deltaTime)
        {
            UpdateSingleEyeVirtualTarget(leftEyeBall, leftEyeVirtualTarget, deltaTime,
                ref leftEyeVtSmoothed, ref leftEyeVtSeeded);
            UpdateSingleEyeVirtualTarget(rightEyeBall, rightEyeVirtualTarget, deltaTime,
                ref rightEyeVtSmoothed, ref rightEyeVtSeeded);
        }

        /// <summary>
        /// Move one eye's virtual target toward the real look target, clamped to a minimum distance
        /// (prevents cross-eye when the target is very close).
        /// </summary>
        private void UpdateSingleEyeVirtualTarget(Transform eyeBall, Transform virtualTarget, float deltaTime,
            ref Vector3 smoothed, ref bool seeded)
        {
            if (eyeBall == null || virtualTarget == null)
                return;

            Vector3 directionToTarget = target.position - eyeBall.position;
            float sqrDistance = directionToTarget.sqrMagnitude;

            Vector3 targetPos = sqrDistance < minDistanceSqr
                ? eyeBall.position + directionToTarget.normalized * minDistance
                : target.position;

            if (!seeded)
            {
                smoothed = virtualTarget.position;
                seeded = true;
            }

            smoothed = Vector3.Lerp(smoothed, targetPos, Mathf.Clamp01(eyeSpeed * deltaTime));
            virtualTarget.position = smoothed;
        }
    }
}
