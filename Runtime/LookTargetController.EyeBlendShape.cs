using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// BlendShape eye control: initialization, calculation, and application
    /// </summary>
    public partial class LookTargetController
    {
        // Euler angle threshold for determining up/down and in/out direction
        private const float EULER_HALF_ROTATION = 180f;
        private const float EULER_FULL_ROTATION = 360f;

        // BlendShape weight range
        private const float BLEND_SHAPE_MAX = 100f;

        // Fade-out completion threshold
        private const float FADE_OUT_THRESHOLD = 0.1f;

        #region BlendShape Eye Control

        /// <summary>
        /// Initialize BlendShape values (called in Update phase)
        /// </summary>
        private void InitializeBlendShapeValues()
        {
            // Detect if eye control was just enabled or disabled
            bool eyeControlJustEnabled = enableEyeControl && !prevEnableEyeControl;
            bool eyeControlJustDisabled = !enableEyeControl && prevEnableEyeControl;
            prevEnableEyeControl = enableEyeControl;

            // Start fade-out when disabled
            if (eyeControlJustDisabled)
            {
                isEyeFadingOut = true;
            }

            // Initialize BlendShape values
            if (enableEyeControl && eyeBlendShapes != null)
            {
                void InitializeList(List<EyeBlendShape> list)
                {
                    if (list == null) return;

                    foreach (var item in list)
                    {
                        if (item?.skinnedMeshRenderer == null) continue;

                        // Re-seed on every enable (so the fade starts from where the face actually is),
                        // and once on first sight of an entry.
                        if (eyeControlJustEnabled || !item.prevInitialized)
                        {
                            item.prevValue = item.skinnedMeshRenderer.GetBlendShapeWeight(item.blendShapeIdx);
                            item.prevInitialized = true;
                        }
                    }
                }

                InitializeList(eyeBlendShapes.eyeLookDownLeftIdx);
                InitializeList(eyeBlendShapes.eyeLookUpLeftIdx);
                InitializeList(eyeBlendShapes.eyeLookInLeftIdx);
                InitializeList(eyeBlendShapes.eyeLookOutLeftIdx);
                InitializeList(eyeBlendShapes.eyeLookDownRightIdx);
                InitializeList(eyeBlendShapes.eyeLookUpRightIdx);
                InitializeList(eyeBlendShapes.eyeLookInRightIdx);
                InitializeList(eyeBlendShapes.eyeLookOutRightIdx);
            }
        }

        /// <summary>
        /// Update BlendShape-based eye control (called in LateUpdate phase)
        /// </summary>
        private void UpdateBlendShapeEyeControl(float deltaTime)
        {
            if (!enableEyeControl && !isEyeFadingOut)
                return;

            if (currentSetting == null)
                return;

            // Nullable ValueTuple: still a struct, so "no angles this frame" costs no allocation.
            // Do not collapse this to a bare ValueTuple — default((Quaternion, Quaternion)) is four
            // zeroed quaternions, which would silently read as a real rotation.
            (Quaternion, Quaternion)? eyeAngles = null;

            switch (currentSetting.eyeStrategy)
            {
                case ELookTargetStrategy.FocusedOnTarget:
                    if (target != null)
                    {
                        GetTargetEyeLocalRotation(out var angles);
                        eyeAngles = angles;
                    }
                    else
                    {
                        eyeAngles = (Quaternion.identity, Quaternion.identity);
                    }
                    break;

                case ELookTargetStrategy.LookIntoVoid:
                    StaringIntoSpaceEye(out var voidAngles);
                    eyeAngles = voidAngles;
                    break;
            }

            if (eyeAngles.HasValue)
            {
                ApplyBlendShapeValues(eyeAngles.Value, deltaTime);
            }
        }

        #endregion

        #region BlendShape Helper Methods

        /// <summary>
        /// Eye rotation local to the head, used to drive the look blend shapes.
        /// </summary>
        /// <remarks>
        /// This used to also hand back a second "quatForEyeTransform" pair (the eye bones' final local
        /// rotations). Its only caller discarded it with <c>out _</c> — the Transform eye strategies are
        /// driven by the direct-aim solver, not from here — so it and the corrected-basis math feeding it
        /// were dead. Removed.
        /// </remarks>
        private bool GetTargetEyeLocalRotation(out (Quaternion, Quaternion) quatLocal)
        {
            // Use eyeVirtualTarget if available (smooth tracking), otherwise fallback to actual target
            Transform targetToUse = eyeVirtualTarget != null ? eyeVirtualTarget : target;

            // Calculate direction from eyes to virtual target
            Vector3 forwardLeftEyeWorld = quatEyeVariance * (targetToUse.position - leftEyeBall.position);
            Vector3 forwardRightEyeWorld = quatEyeVariance * (targetToUse.position - rightEyeBall.position);

            Vector3 forwardLeftEyeLocalBaseHead = head.InverseTransformDirection(forwardLeftEyeWorld);
            Vector3 forwardRightEyeLocalBaseHead = head.InverseTransformDirection(forwardRightEyeWorld);
            Quaternion quatLeftEye = Quaternion.LookRotation(forwardLeftEyeLocalBaseHead, Vector3.up);
            Quaternion quatRightEye = Quaternion.LookRotation(forwardRightEyeLocalBaseHead, Vector3.up);

            float degreeLeftEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatLeftEye);
            float degreeRightEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatRightEye);

            float eyeAngleLimitAdjusted = eyeAngleLimit + (isEyeTracking ? eyeAngleLimitThreshold : 0.0f);

            if (degreeLeftEyeRotate < eyeAngleLimitAdjusted && degreeRightEyeRotate < eyeAngleLimitAdjusted)
            {
                quatLocal = (quatLeftEye, quatRightEye);
                isEyeTracking = true;
                return true;
            }
            else
            {
                quatLocal = (Quaternion.identity, Quaternion.identity);
                isEyeTracking = false;
                return false;
            }
        }

        private void StaringIntoSpaceEye(out (Quaternion, Quaternion) quatLocal)
        {
            Quaternion quatEye = quatEyeVariance * Quaternion.Euler(currentSetting.eyeLookIntoVoid.x, currentSetting.eyeLookIntoVoid.y, 0);

            Vector3 forwardEyeWorld = head.rotation * quatEye * Vector3.forward;

            Vector3 forwardLeftEyeLocalBaseHead = head.InverseTransformDirection(forwardEyeWorld);
            Vector3 forwardRightEyeLocalBaseHead = head.InverseTransformDirection(forwardEyeWorld);
            Quaternion quatLeftEye = Quaternion.LookRotation(forwardLeftEyeLocalBaseHead, Vector3.up);
            Quaternion quatRightEye = Quaternion.LookRotation(forwardRightEyeLocalBaseHead, Vector3.up);

            quatLocal = (quatLeftEye, quatRightEye);
        }

        private void ApplyBlendShapeValues((Quaternion, Quaternion) eyeAngles, float deltaTime)
        {
            Vector3 rotResultLeft = eyeAngles.Item1.eulerAngles;
            Vector3 rotResultRight = eyeAngles.Item2.eulerAngles;

            // If fading out, override to neutral
            if (isEyeFadingOut && !enableEyeControl)
            {
                rotResultLeft = Vector3.zero;
                rotResultRight = Vector3.zero;
            }

            void SetEyeBlendShape(List<EyeBlendShape> list, float value)
            {
                if (list == null) return;

                foreach (var item in list)
                {
                    if (item?.skinnedMeshRenderer == null) continue;

                    float combinedScale = item.scale * eyeBlendShapes.globalScale;
                    float newValue = Mathf.Lerp(item.prevValue, value * combinedScale, eyeSpeed * deltaTime);
                    newValue = Mathf.Clamp(newValue, 0, BLEND_SHAPE_MAX);

                    item.prevValue = newValue;
                    item.prevInitialized = true;

                    // Always write, even when the value has not moved. This write is a deliberate
                    // last-writer: Talkmotion's face transition rewrites every blend shape on the face
                    // renderer, eye-look indices included, so skipping a no-op write would leave the
                    // eyes wherever that transition put them. Writing the opposite axis to 0 is likewise
                    // load-bearing, not waste — it cancels the antagonist shape.
                    item.skinnedMeshRenderer.SetBlendShapeWeight(item.blendShapeIdx, newValue);
                }
            }

            // Left eye - vertical (X axis)
            if (rotResultLeft.x < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, rotResultLeft.x);
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, 0);
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultLeft.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, temp);
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, 0);
            }

            // Left eye - horizontal (Y axis)
            if (rotResultLeft.y < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, rotResultLeft.y);
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, 0);
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultLeft.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, temp);
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, 0);
            }

            // Right eye - vertical (X axis)
            if (rotResultRight.x < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, rotResultRight.x);
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, 0);
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultRight.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, temp);
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, 0);
            }

            // Right eye - horizontal (Y axis)
            if (rotResultRight.y < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, rotResultRight.y);
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, 0);
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultRight.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, temp);
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, 0);
            }

            // Check if fade-out is complete
            if (isEyeFadingOut && AllEyeBlendShapesNearZero())
            {
                isEyeFadingOut = false;
                ResetEyeBlendShapePrevValues();
            }
        }

        /// <summary>
        /// True once every driven blend shape has faded to (near) zero.
        /// </summary>
        private bool AllEyeBlendShapesNearZero()
        {
            var s = eyeBlendShapes;
            if (s == null) return true;

            return ListNearZero(s.eyeLookDownLeftIdx) && ListNearZero(s.eyeLookUpLeftIdx)
                && ListNearZero(s.eyeLookInLeftIdx) && ListNearZero(s.eyeLookOutLeftIdx)
                && ListNearZero(s.eyeLookDownRightIdx) && ListNearZero(s.eyeLookUpRightIdx)
                && ListNearZero(s.eyeLookInRightIdx) && ListNearZero(s.eyeLookOutRightIdx);
        }

        private static bool ListNearZero(List<EyeBlendShape> list)
        {
            if (list == null) return true;

            foreach (var item in list)
            {
                // Skip exactly what the writer skips. An entry with no renderer is never driven, and an
                // entry never initialized holds no value to fade — counting either would hold
                // isEyeFadingOut true forever.
                if (item?.skinnedMeshRenderer == null || !item.prevInitialized) continue;

                if (Mathf.Abs(item.prevValue) > FADE_OUT_THRESHOLD)
                    return false;
            }
            return true;
        }

        private void ResetEyeBlendShapePrevValues()
        {
            var s = eyeBlendShapes;
            if (s == null) return;

            // Only the stored value is cleared; the renderer weight is left as the fade left it,
            // matching the previous behaviour.
            ResetList(s.eyeLookDownLeftIdx);
            ResetList(s.eyeLookUpLeftIdx);
            ResetList(s.eyeLookInLeftIdx);
            ResetList(s.eyeLookOutLeftIdx);
            ResetList(s.eyeLookDownRightIdx);
            ResetList(s.eyeLookUpRightIdx);
            ResetList(s.eyeLookInRightIdx);
            ResetList(s.eyeLookOutRightIdx);
        }

        private static void ResetList(List<EyeBlendShape> list)
        {
            if (list == null) return;

            foreach (var item in list)
            {
                if (item != null)
                    item.prevValue = 0f;
            }
        }

        #endregion
    }
}
