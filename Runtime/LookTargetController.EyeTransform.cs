using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
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

            eyeVirtualTarget.position = Vector3.Lerp(eyeVirtualTarget.position, targetPos, eyeSpeed * deltaTime);
        }

        /// <summary>
        /// Update left/right eye virtual targets with rotation correction (TransformCorrected mode)
        /// </summary>
        private void UpdateEyeVirtualTargetsCorrected(float deltaTime)
        {
            // Update left eye virtual target with rotation correction
            if (leftEyeBall != null && leftEyeVirtualTarget != null)
            {
                Vector3 directionToTarget = target.position - leftEyeBall.position;
                float sqrDistance = directionToTarget.sqrMagnitude;

                // Apply minimum distance constraint
                Vector3 targetPos;
                if (sqrDistance < minDistanceSqr)
                {
                    targetPos = leftEyeBall.position + directionToTarget.normalized * minDistance;
                }
                else
                {
                    targetPos = target.position;
                }

                // KEY: Apply rotation correction
                // The eye has an initial rotation (e.g., 2.7, 337, 0.2) when looking forward
                // We need to apply the initial rotation to the target direction
                // So when the eye aims at the corrected target, it actually looks at the real target
                Vector3 correctedDirection = targetPos - leftEyeBall.position;

                // Transform the direction into eye's local space, then apply initial rotation (NOT inverse)
                Vector3 localDirection = leftEyeBall.InverseTransformDirection(correctedDirection);
                Vector3 correctedLocalDirection = initialLeftEyeLocalRotation * localDirection;
                Vector3 correctedWorldDirection = leftEyeBall.TransformDirection(correctedLocalDirection);

                Vector3 correctedTargetPos = leftEyeBall.position + correctedWorldDirection.normalized * correctedDirection.magnitude;
                leftEyeVirtualTarget.position = Vector3.Lerp(leftEyeVirtualTarget.position, correctedTargetPos, eyeSpeed * deltaTime);
            }

            // Update right eye virtual target with rotation correction
            if (rightEyeBall != null && rightEyeVirtualTarget != null)
            {
                Vector3 directionToTarget = target.position - rightEyeBall.position;
                float sqrDistance = directionToTarget.sqrMagnitude;

                // Apply minimum distance constraint
                Vector3 targetPos;
                if (sqrDistance < minDistanceSqr)
                {
                    targetPos = rightEyeBall.position + directionToTarget.normalized * minDistance;
                }
                else
                {
                    targetPos = target.position;
                }

                // KEY: Apply rotation correction (same as left eye)
                Vector3 correctedDirection = targetPos - rightEyeBall.position;

                // Transform the direction into eye's local space, then apply initial rotation (NOT inverse)
                Vector3 localDirection = rightEyeBall.InverseTransformDirection(correctedDirection);
                Vector3 correctedLocalDirection = initialRightEyeLocalRotation * localDirection;
                Vector3 correctedWorldDirection = rightEyeBall.TransformDirection(correctedLocalDirection);

                Vector3 correctedTargetPos = rightEyeBall.position + correctedWorldDirection.normalized * correctedDirection.magnitude;
                rightEyeVirtualTarget.position = Vector3.Lerp(rightEyeVirtualTarget.position, correctedTargetPos, eyeSpeed * deltaTime);
            }
        }
    }
#endif
}
