using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Look target strategy enum
    /// </summary>
    [Serializable]
    public enum ELookTargetStrategy
    {
        LookIntoVoid,
        FocusedOnTarget,
    }

    /// <summary>
    /// Look target setting
    /// </summary>
    [Serializable]
    public class LookTargetSetting
    {
        public ELookTargetStrategy headStrategy;
        public Vector2 headLookIntoVoid;
        public Vector2 headAngleVariance;

        public ELookTargetStrategy eyeStrategy;
        public Vector2 eyeLookIntoVoid;
        public Vector2 eyeAngleVariance;
    }

    /// <summary>
    /// Eye control strategy enum
    /// </summary>
    public enum EEyeControlStrategy
    {
        BlendWeightFluentt,
        Transform,
        TransformCorrected,
    }

    /// <summary>
    /// Eye blend shape data
    /// </summary>
    [System.Serializable]
    public class EyeBlendShape
    {
        public SkinnedMeshRenderer skmr;
        public string blendShapeName;
        public int blendShapeIdx;
        public float scale;
    }

    /// <summary>
    /// Eye blend shapes collection
    /// </summary>
    [System.Serializable]
    public class EyeBlendShapes
    {
        [Range(0f, 10f)]
        [Tooltip("Global scale applied to all eye blend shapes")]
        public float globalScale = 1.0f;

        public List<EyeBlendShape> eyeLookUpLeftIdx;
        public List<EyeBlendShape> eyeLookDownLeftIdx;
        public List<EyeBlendShape> eyeLookInLeftIdx;
        public List<EyeBlendShape> eyeLookOutLeftIdx;
        public List<EyeBlendShape> eyeLookUpRightIdx;
        public List<EyeBlendShape> eyeLookDownRightIdx;
        public List<EyeBlendShape> eyeLookInRightIdx;
        public List<EyeBlendShape> eyeLookOutRightIdx;
    }

    /// <summary>
    /// Simple look target controller without timeline dependency
    /// Manages virtual targets and constraints for smooth look-at behavior
    /// Supports both Transform and BlendShape eye control strategies
    /// </summary>
    [System.Serializable]
    public class LookTargetController
    {
        // Animation Rigging Multi-Aim Constraints (Transform strategy)
        private MultiAimConstraint headAimConstraint;
        private MultiAimConstraint leftEyeAimConstraint;
        private MultiAimConstraint rightEyeAimConstraint;

        // Actual look target
        private Transform target;

        // Virtual targets for smooth tracking (Slerp-based)
        private Transform headVirtualTarget;
        private Transform eyeVirtualTarget; // Single target for both eyes (Transform mode)
        private Transform leftEyeVirtualTarget; // Left eye target (TransformCorrected mode)
        private Transform rightEyeVirtualTarget; // Right eye target (TransformCorrected mode)
        private float minDistance = 0.5f; // Minimum distance to prevent cross-eye
        private float minDistanceSqr; // Cached squared value for performance

        // Base transforms (head and eyes)
        private Transform head;
        private Transform leftEyeBall;
        private Transform rightEyeBall;

        // Control settings
        public bool enableHeadControl = true;
        public bool enableEyeControl = false;
        public float headSpeed = 5f;
        public float eyeSpeed = 10f;

        // Eye control strategy
        public EEyeControlStrategy eyeControlStrategy = EEyeControlStrategy.Transform;
        public EyeBlendShapes eyeBlendShapes = new();
        public float eyeAngleLimit = 10f;
        public float eyeAngleLimitThreshold = 5f;
        private bool isLookingEye = false;

        // Eye rotation helpers for BlendShape strategy
        private Quaternion quadLeftEyeBallFromHead;
        private Quaternion quadRightEyeBallFromHead;
        private Quaternion initialLeftEyeLocalRotation;
        private Quaternion initialRightEyeLocalRotation;

        // BlendShape state tracking
        private Dictionary<string, float> eyeBlendShapePrevValues = new Dictionary<string, float>();
        private bool prevEnableEyeControl = false;
        private bool isFadingOutEye = false;

        // Look target settings
        private LookTargetSetting currentSetting;
        private Quaternion quatEyeVariance = Quaternion.identity;

        // Public getters for Gizmo visualization
        public Transform HeadVirtualTarget => headVirtualTarget;
        public Transform EyeVirtualTarget => eyeVirtualTarget;
        public Transform LeftEyeVirtualTarget => leftEyeVirtualTarget;
        public Transform RightEyeVirtualTarget => rightEyeVirtualTarget;

        /// <summary>
        /// Set the Multi-Aim Constraint for head tracking
        /// </summary>
        public void SetHeadAimConstraint(MultiAimConstraint constraint)
        {
            headAimConstraint = constraint;
        }

        /// <summary>
        /// Set the Multi-Aim Constraint for left eye tracking
        /// </summary>
        public void SetLeftEyeAimConstraint(MultiAimConstraint constraint)
        {
            leftEyeAimConstraint = constraint;
        }

        /// <summary>
        /// Set the Multi-Aim Constraint for right eye tracking
        /// </summary>
        public void SetRightEyeAimConstraint(MultiAimConstraint constraint)
        {
            rightEyeAimConstraint = constraint;
        }

        /// <summary>
        /// Set base transforms (head and eyes)
        /// </summary>
        public void SetTransforms(Transform head, Transform leftEye, Transform rightEye)
        {
            this.head = head;
            this.leftEyeBall = leftEye;
            this.rightEyeBall = rightEye;

            // Initialize eye ball rotations for BlendShape strategy
            if (leftEyeBall != null && rightEyeBall != null && head != null)
            {
                quadLeftEyeBallFromHead = Quaternion.Inverse(head.rotation) * leftEyeBall.parent.rotation;
                quadRightEyeBallFromHead = Quaternion.Inverse(head.rotation) * rightEyeBall.parent.rotation;
                initialLeftEyeLocalRotation = leftEyeBall.localRotation;
                initialRightEyeLocalRotation = rightEyeBall.localRotation;
            }
        }

        /// <summary>
        /// Set the actual look target
        /// </summary>
        public void SetLookTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Set look target setting (for strategy and variance)
        /// </summary>
        public void SetLookTargetSetting(LookTargetSetting setting)
        {
            currentSetting = setting;

            // Initialize eye angle variance
            if (setting != null)
            {
                Vector3 angle = setting.eyeAngleVariance;
                float randomPitch = UnityEngine.Random.Range(-angle.x, angle.x);
                float randomYaw = UnityEngine.Random.Range(-angle.y, angle.y);
                quatEyeVariance = Quaternion.Euler(randomPitch, randomYaw, 0);
            }
        }

        /// <summary>
        /// Set virtual targets directly (avoids GameObject.Find() call)
        /// For Transform mode - single eye target
        /// </summary>
        public void SetVirtualTargets(Transform head, Transform eye)
        {
            headVirtualTarget = head;
            eyeVirtualTarget = eye;
        }

        /// <summary>
        /// Set virtual targets directly for TransformCorrected mode - separate left/right eye targets
        /// </summary>
        public void SetVirtualTargetsCorrected(Transform head, Transform leftEye, Transform rightEye)
        {
            headVirtualTarget = head;
            leftEyeVirtualTarget = leftEye;
            rightEyeVirtualTarget = rightEye;
        }

        /// <summary>
        /// Initialize virtual targets - find them from avatar root children
        /// </summary>
        /// <param name="useFindMethod">If true, uses GameObject.Find() to locate virtual targets. If false, requires SetVirtualTargets() to be called manually.</param>
        public void Initialize(bool useFindMethod = true)
        {
            // Cache squared distance for performance
            minDistanceSqr = minDistance * minDistance;

            // Find virtual targets only if requested (avoids expensive GameObject.Find)
            if (useFindMethod)
            {
                FindVirtualTargets();
            }
        }

        /// <summary>
        /// Find virtual targets from VirtualTargets container in scene root
        /// </summary>
        private void FindVirtualTargets()
        {
            // Find avatar root: constraint -> TargetTracking -> Avatar
            Transform avatarRoot = null;
            if (headAimConstraint != null)
            {
                // HeadTracking -> TargetTracking -> Avatar
                avatarRoot = headAimConstraint.transform.parent?.parent;
            }

            if (avatarRoot == null)
            {
                Debug.LogWarning("[LookTargetController] Cannot find avatar root!");
                return;
            }

            // Find VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer == null)
            {
                Debug.LogWarning("[LookTargetController] VirtualTargets container not found! Please enable Look Target in Editor to auto-create.");
                return;
            }

            // Find avatar-specific virtual target group
            string avatarGroupName = $"{avatarRoot.name}_VirtualTargets";
            Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
            if (avatarVirtualTargetGroup == null)
            {
                Debug.LogWarning($"[LookTargetController] {avatarGroupName} not found! Please enable Look Target in Editor to auto-create.");
                return;
            }

            // Find head virtual target
            if (headVirtualTarget == null)
            {
                headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
                if (headVirtualTarget != null)
                {
                    Debug.Log("[LookTargetController] Found HeadVirtualTarget");
                }
                else
                {
                    Debug.LogWarning("[LookTargetController] HeadVirtualTarget not found!");
                }
            }

            // Find eye virtual target (single target for both eyes - Transform mode)
            if (eyeVirtualTarget == null)
            {
                eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
                if (eyeVirtualTarget != null)
                {
                    Debug.Log("[LookTargetController] Found EyeVirtualTarget");
                }
                else
                {
                    Debug.LogWarning("[LookTargetController] EyeVirtualTarget not found!");
                }
            }

            // Find left/right eye virtual targets (TransformCorrected mode)
            if (leftEyeVirtualTarget == null)
            {
                leftEyeVirtualTarget = avatarVirtualTargetGroup.Find("LeftEyeVirtualTarget");
                if (leftEyeVirtualTarget != null)
                {
                    Debug.Log("[LookTargetController] Found LeftEyeVirtualTarget");
                }
            }

            if (rightEyeVirtualTarget == null)
            {
                rightEyeVirtualTarget = avatarVirtualTargetGroup.Find("RightEyeVirtualTarget");
                if (rightEyeVirtualTarget != null)
                {
                    Debug.Log("[LookTargetController] Found RightEyeVirtualTarget");
                }
            }
        }

        /// <summary>
        /// Update virtual targets to follow actual target
        /// Call this every frame (Update phase for Transform strategy, or BlendShape initialization)
        /// </summary>
        public void Update(float deltaTime)
        {
            if (target == null)
                return;

            // Update head virtual target when enabled
            if (enableHeadControl)
            {
                UpdateHeadVirtualTarget(deltaTime);
            }

            // Update eye virtual targets and initialize BlendShape values when enabled
            if (enableEyeControl)
            {
                if (eyeControlStrategy == EEyeControlStrategy.Transform)
                {
                    UpdateEyeVirtualTarget(deltaTime);
                }
                else if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    UpdateEyeVirtualTargetsCorrected(deltaTime);
                }
                else if (eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
                {
                    // BlendWeight strategy also needs virtual target for direction calculation
                    UpdateEyeVirtualTarget(deltaTime);
                    InitializeBlendShapeValues();
                }
            }

            // Update constraint weights
            UpdateConstraintWeights();
        }

        /// <summary>
        /// LateUpdate for eye control (should be called in LateUpdate phase)
        /// BlendShape strategy uses this, Transform strategy handled in Update
        /// </summary>
        public void LateUpdate(float deltaTime)
        {
            if (target == null)
                return;

            // Only process BlendShape strategy in LateUpdate
            if (eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                UpdateBlendShapeEyeControl(deltaTime);
            }
        }

        /// <summary>
        /// Update head virtual target to follow actual target
        /// </summary>
        private void UpdateHeadVirtualTarget(float deltaTime)
        {
            if (head == null || headVirtualTarget == null)
                return;

            Vector3 directionToTarget = target.position - head.position;
            float sqrDistance = directionToTarget.sqrMagnitude;

            Vector3 targetPos;
            if (sqrDistance < minDistanceSqr)
            {
                // Normalize only when needed
                targetPos = head.position + directionToTarget.normalized * minDistance;
            }
            else
            {
                targetPos = target.position;
            }

            headVirtualTarget.position = Vector3.Lerp(headVirtualTarget.position, targetPos, headSpeed * deltaTime);
        }

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

        /// <summary>
        /// Update constraint weights based on enable flags
        /// </summary>
        private void UpdateConstraintWeights()
        {
            if (headAimConstraint != null)
            {
                headAimConstraint.weight = enableHeadControl ? 1f : 0f;
            }

            if (leftEyeAimConstraint != null)
            {
                leftEyeAimConstraint.weight = enableEyeControl ? 1f : 0f;
            }

            if (rightEyeAimConstraint != null)
            {
                rightEyeAimConstraint.weight = enableEyeControl ? 1f : 0f;
            }
        }

        /// <summary>
        /// Enable look target tracking
        /// </summary>
        public void Enable()
        {
            UpdateConstraintWeights();
        }

        /// <summary>
        /// Disable look target tracking
        /// </summary>
        public void Disable()
        {
            if (headAimConstraint != null)
            {
                headAimConstraint.weight = 0f;
            }

            if (leftEyeAimConstraint != null)
            {
                leftEyeAimConstraint.weight = 0f;
            }

            if (rightEyeAimConstraint != null)
            {
                rightEyeAimConstraint.weight = 0f;
            }
        }

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
                isFadingOutEye = true;
            }

            // Initialize BlendShape values
            if (enableEyeControl && eyeBlendShapes != null)
            {
                void InitializeList(List<EyeBlendShape> list, string keyPrefix)
                {
                    if (list == null) return;

                    foreach (var item in list)
                    {
                        if (item?.skmr == null) continue;

                        string key = $"{keyPrefix}_{item.skmr.GetInstanceID()}_{item.blendShapeIdx}";
                        if (eyeControlJustEnabled || !eyeBlendShapePrevValues.ContainsKey(key))
                        {
                            eyeBlendShapePrevValues[key] = item.skmr.GetBlendShapeWeight(item.blendShapeIdx);
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
            if (!enableEyeControl && !isFadingOutEye)
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

            Vector3 vecForwardLeftEyeLocalCorrected = quadLeftEyeBallFromHead * forwardLeftEyeLocal;
            Vector3 vecForwardRightEyeLocalCorrected = quadRightEyeBallFromHead * forwardRightEyeLocal;

            Quaternion quatLeftEyeCorrected = Quaternion.LookRotation(vecForwardLeftEyeLocalCorrected, Vector3.up);
            Quaternion quatRightEyeCorrected = Quaternion.LookRotation(vecForwardRightEyeLocalCorrected, Vector3.up);

            Vector3 forwardLeftEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardLeftEyeWorld);
            Vector3 forwardRightEyeLocalBaseHead = head.transform.InverseTransformDirection(forwardRightEyeWorld);
            Quaternion quatLeftEye = Quaternion.LookRotation(forwardLeftEyeLocalBaseHead, Vector3.up);
            Quaternion quatRightEye = Quaternion.LookRotation(forwardRightEyeLocalBaseHead, Vector3.up);

            float degreeLeftEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatLeftEye);
            float degreeRightEyeRotate = Quaternion.Angle(Quaternion.Euler(Vector3.forward), quatRightEye);

            float eyeAngleLimitAdjusted = eyeAngleLimit + (isLookingEye ? eyeAngleLimitThreshold : 0.0f);

            if (degreeLeftEyeRotate < eyeAngleLimitAdjusted && degreeRightEyeRotate < eyeAngleLimitAdjusted)
            {
                Quaternion finalLeftRotation = initialLeftEyeLocalRotation * quadLeftEyeBallFromHead * quatLeftEyeCorrected;
                Quaternion finalRightRotation = initialRightEyeLocalRotation * quadRightEyeBallFromHead * quatRightEyeCorrected;

                quatForEyeTransform = new Tuple<Quaternion, Quaternion>(finalLeftRotation, finalRightRotation);
                quatLocal = new Tuple<Quaternion, Quaternion>(quatLeftEye, quatRightEye);

                isLookingEye = true;
                return true;
            }
            else
            {
                quatForEyeTransform = new Tuple<Quaternion, Quaternion>(initialLeftEyeLocalRotation, initialRightEyeLocalRotation);
                quatLocal = new Tuple<Quaternion, Quaternion>(Quaternion.identity, Quaternion.identity);

                isLookingEye = false;
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
            if (isFadingOutEye && !enableEyeControl)
            {
                rotResultLeft = Vector3.zero;
                rotResultRight = Vector3.zero;
            }

            void SetEyeBlendShape(List<EyeBlendShape> list, float value, string keyPrefix)
            {
                if (list == null) return;

                foreach (var item in list)
                {
                    if (item?.skmr == null) continue;

                    string key = $"{keyPrefix}_{item.skmr.GetInstanceID()}_{item.blendShapeIdx}";
                    float prevValue = eyeBlendShapePrevValues.GetValueOrDefault(key, 0f);

                    float combinedScale = item.scale * eyeBlendShapes.globalScale;
                    float newValue = Mathf.Lerp(prevValue, value * combinedScale, eyeSpeed * deltaTime);
                    newValue = Mathf.Clamp(newValue, 0, 100);

                    eyeBlendShapePrevValues[key] = newValue;
                    item.skmr.SetBlendShapeWeight(item.blendShapeIdx, newValue);
                }
            }

            // Left eye - vertical (X axis)
            if (rotResultLeft.x < 180)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, rotResultLeft.x, "eyeLookDownLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, 0, "eyeLookUpLeft");
            }
            else
            {
                float temp = 360 - rotResultLeft.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpLeftIdx, temp, "eyeLookUpLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownLeftIdx, 0, "eyeLookDownLeft");
            }

            // Left eye - horizontal (Y axis)
            if (rotResultLeft.y < 180)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, rotResultLeft.y, "eyeLookInLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, 0, "eyeLookOutLeft");
            }
            else
            {
                float temp = 360 - rotResultLeft.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutLeftIdx, temp, "eyeLookOutLeft");
                SetEyeBlendShape(eyeBlendShapes.eyeLookInLeftIdx, 0, "eyeLookInLeft");
            }

            // Right eye - vertical (X axis)
            if (rotResultRight.x < 180)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, rotResultRight.x, "eyeLookDownRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, 0, "eyeLookUpRight");
            }
            else
            {
                float temp = 360 - rotResultRight.x;
                SetEyeBlendShape(eyeBlendShapes.eyeLookUpRightIdx, temp, "eyeLookUpRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookDownRightIdx, 0, "eyeLookDownRight");
            }

            // Right eye - horizontal (Y axis)
            if (rotResultRight.y < 180)
            {
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, rotResultRight.y, "eyeLookOutRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, 0, "eyeLookInRight");
            }
            else
            {
                float temp = 360 - rotResultRight.y;
                SetEyeBlendShape(eyeBlendShapes.eyeLookInRightIdx, temp, "eyeLookInRight");
                SetEyeBlendShape(eyeBlendShapes.eyeLookOutRightIdx, 0, "eyeLookOutRight");
            }

            // Check if fade-out is complete
            if (isFadingOutEye)
            {
                bool allNearZero = true;
                foreach (var kvp in eyeBlendShapePrevValues)
                {
                    if (Mathf.Abs(kvp.Value) > 0.1f)
                    {
                        allNearZero = false;
                        break;
                    }
                }

                if (allNearZero)
                {
                    isFadingOutEye = false;
                    foreach (var key in eyeBlendShapePrevValues.Keys.ToList())
                    {
                        eyeBlendShapePrevValues[key] = 0f;
                    }
                }
            }
        }

        #endregion
    }
}
