using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
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
                void InitializeList(List<EyeBlendShape> list, string keyPrefix)
                {
                    if (list == null) return;

                    foreach (var item in list)
                    {
                        if (item?.skinnedMeshRenderer == null) continue;

                        string key = $"{keyPrefix}_{item.skinnedMeshRenderer.GetInstanceID()}_{item.blendShapeIdx}";
                        if (eyeControlJustEnabled || !eyeBlendShapePrevValues.ContainsKey(key))
                        {
                            eyeBlendShapePrevValues[key] = item.skinnedMeshRenderer.GetBlendShapeWeight(item.blendShapeIdx);
                        }
                    }
                }

                InitializeList(eyeBlendShapes.eyeLookDownLeftIdx, "eyeLookDownLeft");
                InitializeList(eyeBlendShapes.eyeLookUpLeftIdx, "eyeLookUpLeft");
                InitializeList(eyeBlendShapes.eyeLookInLeftIdx, "eyeLookInLeft");
                InitializeList(eyeBlendShapes.eyeLookOutLeftIdx, "eyeLookOutLeft");
                InitializeList(eyeBlendShapes.eyeLookDownRightIdx, "eyeLookDownRight");
                InitializeList(eyeBlendShapes.eyeLookUpRightIdx, "eyeLookUpRight");
                InitializeList(eyeBlendShapes.eyeLookInRightIdx, "eyeLookInRight");
                InitializeList(eyeBlendShapes.eyeLookOutRightIdx, "eyeLookOutRight");
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

            Tuple<Quaternion, Quaternion> eyeAngles = null;

            switch (currentSetting.eyeStrategy)
            {
                case ELookTargetStrategy.FocusedOnTarget:
                    if (target != null)
                    {
                        GetTargetEyeLocalRotation(out _, out eyeAngles);
                    }
                    else
                    {
                        eyeAngles = new Tuple<Quaternion, Quaternion>(Quaternion.identity, Quaternion.identity);
                    }
                    break;

                case ELookTargetStrategy.LookIntoVoid:
                    StaringIntoSpaceEye(out eyeAngles);
                    break;
            }

            if (eyeAngles != null)
            {
                ApplyBlendShapeValues(eyeAngles, deltaTime);
            }
        }

        #endregion

        #region BlendShape Helper Methods

        private bool GetTargetEyeLocalRotation(
            out Tuple<Quaternion, Quaternion> quatForEyeTransform,
            out Tuple<Quaternion, Quaternion> quatLocal)
        {
            // Use eyeVirtualTarget if available (smooth tracking), otherwise fallback to actual target
            Transform targetToUse = eyeVirtualTarget != null ? eyeVirtualTarget : target;

            // Calculate direction from eyes to virtual target
            Vector3 forwardLeftEyeWorld = quatEyeVariance * (targetToUse.position - leftEyeBall.position);
            Vector3 forwardRightEyeWorld = quatEyeVariance * (targetToUse.position - rightEyeBall.position);

            Vector3 forwardLeftEyeLocal = leftEyeBall.parent.transform.InverseTransformDirection(forwardLeftEyeWorld);
            Vector3 forwardRightEyeLocal = rightEyeBall.parent.transform.InverseTransformDirection(forwardRightEyeWorld);

            Vector3 vecForwardLeftEyeLocalCorrected = rotLeftEyeFromHead * forwardLeftEyeLocal;
            Vector3 vecForwardRightEyeLocalCorrected = rotRightEyeFromHead * forwardRightEyeLocal;

            Quaternion quatLeftEyeCorrected = Quaternion.LookRotation(vecForwardLeftEyeLocalCorrected, Vector3.up);
            Quaternion quatRightEyeCorrected = Quaternion.LookRotation(vecForwardRightEyeLocalCorrected, Vector3.up);

            Vector3 forwardLeftEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardLeftEyeWorld);
            Vector3 forwardRightEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardRightEyeWorld);
            Quaternion quatLeftEye = Quaternion.LookRotation(forwardLeftEyeLocalBaseHead, Vector3.up);
            Quaternion quatRightEye = Quaternion.LookRotation(forwardRightEyeLocalBaseHead, Vector3.up);

            float degreeLeftEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatLeftEye);
            float degreeRightEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatRightEye);

            float eyeAngleLimitAdjusted = eyeAngleLimit + (isEyeTracking ? eyeAngleLimitThreshold : 0.0f);

            if (degreeLeftEyeRotate < eyeAngleLimitAdjusted && degreeRightEyeRotate < eyeAngleLimitAdjusted)
            {
                Quaternion finalLeftRotation = initialLeftEyeLocalRotation * rotLeftEyeFromHead * quatLeftEyeCorrected;
                Quaternion finalRightRotation = initialRightEyeLocalRotation * rotRightEyeFromHead * quatRightEyeCorrected;

                quatForEyeTransform = new Tuple<Quaternion, Quaternion>(finalLeftRotation, finalRightRotation);
                quatLocal = new Tuple<Quaternion, Quaternion>(quatLeftEye, quatRightEye);

                isEyeTracking = true;
                return true;
            }
            else
            {
                quatForEyeTransform = new Tuple<Quaternion, Quaternion>(initialLeftEyeLocalRotation, initialRightEyeLocalRotation);
                quatLocal = new Tuple<Quaternion, Quaternion>(Quaternion.identity, Quaternion.identity);

                isEyeTracking = false;
                return false;
            }
        }

        private void StaringIntoSpaceEye(out Tuple<Quaternion, Quaternion> quatLocal)
        {
            Quaternion quatEye = quatEyeVariance * Quaternion.Euler(currentSetting.eyeLookIntoVoid.x, currentSetting.eyeLookIntoVoid.y, 0);

            Vector3 forwardEyeWorld = head.rotation * quatEye * Vector3.forward;

            Vector3 forwardLeftEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardEyeWorld);
            Vector3 forwardRightEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardEyeWorld);
            Quaternion quatLeftEye = Quaternion.LookRotation(forwardLeftEyeLocalBaseHead, Vector3.up);
            Quaternion quatRightEye = Quaternion.LookRotation(forwardRightEyeLocalBaseHead, Vector3.up);

            quatLocal = new Tuple<Quaternion, Quaternion>(quatLeftEye, quatRightEye);
        }

        private void ApplyBlendShapeValues(Tuple<Quaternion, Quaternion> eyeAngles, float deltaTime)
        {
            Vector3 rotResultLeft = eyeAngles.Item1.eulerAngles;
            Vector3 rotResultRight = eyeAngles.Item2.eulerAngles;

            // If fading out, override to neutral
            if (isEyeFadingOut && !enableEyeControl)
            {
                rotResultLeft = Vector3.zero;
                rotResultRight = Vector3.zero;
            }

            void SetEyeBlendShape(List<EyeBlendShape> list, float value, string keyPrefix)
            {
                if (list == null) return;

                foreach (var item in list)
                {
                    if (item?.skinnedMeshRenderer == null) continue;

                    string key = $"{keyPrefix}_{item.skinnedMeshRenderer.GetInstanceID()}_{item.blendShapeIdx}";
                    float prevValue = eyeBlendShapePrevValues.GetValueOrDefault(key, 0f);

                    float combinedScale = item.scale * eyeBlendShapes.globalScale;
                    float newValue = Mathf.Lerp(prevValue, value * combinedScale, eyeSpeed * deltaTime);
                    newValue = Mathf.Clamp(newValue, 0, BLEND_SHAPE_MAX);

                    eyeBlendShapePrevValues[key] = newValue;
                    item.skinnedMeshRenderer.SetBlendShapeWeight(item.blendShapeIdx, newValue);
                }
            }

            // Left eye - vertical (X axis)
            if (rotResultLeft.x < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, rotResultLeft.x, "eyeLookDownLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, 0, "eyeLookUpLeft");
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultLeft.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, temp, "eyeLookUpLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, 0, "eyeLookDownLeft");
            }

            // Left eye - horizontal (Y axis)
            if (rotResultLeft.y < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, rotResultLeft.y, "eyeLookInLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, 0, "eyeLookOutLeft");
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultLeft.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, temp, "eyeLookOutLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, 0, "eyeLookInLeft");
            }

            // Right eye - vertical (X axis)
            if (rotResultRight.x < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, rotResultRight.x, "eyeLookDownRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, 0, "eyeLookUpRight");
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultRight.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, temp, "eyeLookUpRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, 0, "eyeLookDownRight");
            }

            // Right eye - horizontal (Y axis)
            if (rotResultRight.y < EULER_HALF_ROTATION)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, rotResultRight.y, "eyeLookOutRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, 0, "eyeLookInRight");
            }
            else
            {
                float temp = EULER_FULL_ROTATION - rotResultRight.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, temp, "eyeLookInRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, 0, "eyeLookOutRight");
            }

            // Check if fade-out is complete
            if (isEyeFadingOut)
            {
                bool allNearZero = true;
                foreach (var kvp in eyeBlendShapePrevValues)
                {
                    if (Mathf.Abs(kvp.Value) > FADE_OUT_THRESHOLD)
                    {
                        allNearZero = false;
                        break;
                    }
                }

                if (allNearZero)
                {
                    isEyeFadingOut = false;
                    foreach (var key in eyeBlendShapePrevValues.Keys.ToList())
                    {
                        eyeBlendShapePrevValues[key] = 0f;
                    }
                }
            }
        }

        #endregion
    }
#endif
}
