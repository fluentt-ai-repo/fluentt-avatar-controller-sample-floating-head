using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Look Target Gizmo visualization
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        #region Gizmo Visualization

        private void OnDrawGizmos()
        {
            if (!showTargetGizmos)
                return;

            // Draw actual look target
            if (lookTarget != null)
            {
                Gizmos.color = actualTargetColor;
                Gizmos.DrawWireSphere(lookTarget.position, actualTargetGizmoSize);
                Gizmos.DrawSphere(lookTarget.position, actualTargetGizmoSize * 0.3f);
            }

            // Draw virtual targets if look target controller is initialized.
            // A bone owned by the direct-drive solver has an idle virtual target (see
            // LookTargetController.headDirectDriven), so drawing it at runtime would show a stale
            // position that no longer represents where the bone is aiming. Skip those.
            if (lookTargetController != null)
            {
                bool headVirtualTargetLive = !(Application.isPlaying && _headAimCalibrated);
                bool eyeVirtualTargetLive = !(Application.isPlaying && EyesDirectDriven);

                // Draw head virtual target
                if (headVirtualTargetLive && lookTargetController.HeadVirtualTarget != null)
                {
                    Gizmos.color = headVirtualTargetColor;
                    Gizmos.DrawWireSphere(lookTargetController.HeadVirtualTarget.position, headVirtualTargetGizmoSize);
                    Gizmos.DrawSphere(lookTargetController.HeadVirtualTarget.position, headVirtualTargetGizmoSize * 0.3f);

                    // Draw line from head to virtual target
                    if (lookHead != null)
                    {
                        Gizmos.color = new Color(headVirtualTargetColor.r, headVirtualTargetColor.g, headVirtualTargetColor.b, 0.5f);
                        Gizmos.DrawLine(lookHead.position, lookTargetController.HeadVirtualTarget.position);
                    }
                }
                else if (!headVirtualTargetLive && lookTarget != null && lookHead != null)
                {
                    // Direct-driven head: draw the real aim ray (from the eye midpoint, where it aims).
                    Vector3 aimOrigin = (lookLeftEyeBall != null && lookRightEyeBall != null && lookLeftEyeBall != lookHead)
                        ? (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f
                        : lookHead.position;
                    Gizmos.color = new Color(headVirtualTargetColor.r, headVirtualTargetColor.g, headVirtualTargetColor.b, 0.5f);
                    Gizmos.DrawLine(aimOrigin, lookTarget.position);
                }

                // Draw eye virtual targets
                if (!eyeVirtualTargetLive)
                {
                    // Direct-driven eyes: draw the real gaze rays instead of the idle virtual targets.
                    if (lookTarget != null)
                    {
                        Gizmos.color = new Color(eyeVirtualTargetColor.r, eyeVirtualTargetColor.g, eyeVirtualTargetColor.b, 0.5f);
                        if (lookLeftEyeBall != null)
                            Gizmos.DrawLine(lookLeftEyeBall.position, lookTarget.position);
                        if (lookRightEyeBall != null)
                            Gizmos.DrawLine(lookRightEyeBall.position, lookTarget.position);
                    }
                }
                else if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    // Draw separate left/right eye virtual targets for TransformCorrected mode
                    if (lookTargetController.LeftEyeVirtualTarget != null)
                    {
                        Gizmos.color = eyeVirtualTargetColor;
                        Gizmos.DrawWireSphere(lookTargetController.LeftEyeVirtualTarget.position, eyeVirtualTargetGizmoSize);
                        Gizmos.DrawSphere(lookTargetController.LeftEyeVirtualTarget.position, eyeVirtualTargetGizmoSize * 0.3f);

                        // Draw line from left eye to its virtual target
                        if (lookLeftEyeBall != null)
                        {
                            Gizmos.color = new Color(eyeVirtualTargetColor.r, eyeVirtualTargetColor.g, eyeVirtualTargetColor.b, 0.5f);
                            Gizmos.DrawLine(lookLeftEyeBall.position, lookTargetController.LeftEyeVirtualTarget.position);
                        }
                    }

                    if (lookTargetController.RightEyeVirtualTarget != null)
                    {
                        Gizmos.color = eyeVirtualTargetColor;
                        Gizmos.DrawWireSphere(lookTargetController.RightEyeVirtualTarget.position, eyeVirtualTargetGizmoSize);
                        Gizmos.DrawSphere(lookTargetController.RightEyeVirtualTarget.position, eyeVirtualTargetGizmoSize * 0.3f);

                        // Draw line from right eye to its virtual target
                        if (lookRightEyeBall != null)
                        {
                            Gizmos.color = new Color(eyeVirtualTargetColor.r, eyeVirtualTargetColor.g, eyeVirtualTargetColor.b, 0.5f);
                            Gizmos.DrawLine(lookRightEyeBall.position, lookTargetController.RightEyeVirtualTarget.position);
                        }
                    }
                }
                else
                {
                    // Draw single eye virtual target for Transform/BlendShape modes
                    if (lookTargetController.EyeVirtualTarget != null)
                    {
                        Gizmos.color = eyeVirtualTargetColor;
                        Gizmos.DrawWireSphere(lookTargetController.EyeVirtualTarget.position, eyeVirtualTargetGizmoSize);
                        Gizmos.DrawSphere(lookTargetController.EyeVirtualTarget.position, eyeVirtualTargetGizmoSize * 0.3f);

                        // Draw lines from both eyes to virtual target
                        Gizmos.color = new Color(eyeVirtualTargetColor.r, eyeVirtualTargetColor.g, eyeVirtualTargetColor.b, 0.5f);
                        if (lookLeftEyeBall != null)
                        {
                            Gizmos.DrawLine(lookLeftEyeBall.position, lookTargetController.EyeVirtualTarget.position);
                        }
                        if (lookRightEyeBall != null)
                        {
                            Gizmos.DrawLine(lookRightEyeBall.position, lookTargetController.EyeVirtualTarget.position);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
