using System;
using System.Collections.Generic;
using UnityEngine;
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
using UnityEngine.Animations.Rigging;
#endif

namespace FluentT.Avatar.SampleFloatingHead
{
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
    /// <summary>
    /// Simple look target controller without timeline dependency
    /// Manages virtual targets and constraints for smooth look-at behavior
    /// Supports both Transform and BlendShape eye control strategies
    /// </summary>
    [System.Serializable]
    public partial class LookTargetController
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
        private bool isEyeTracking = false;

        // Eye rotation helpers for BlendShape strategy
        private Quaternion rotLeftEyeFromHead;
        private Quaternion rotRightEyeFromHead;
        private Quaternion initialLeftEyeLocalRotation;
        private Quaternion initialRightEyeLocalRotation;

        // BlendShape state tracking
        private Dictionary<string, float> eyeBlendShapePrevValues = new Dictionary<string, float>();
        private bool prevEnableEyeControl = false;
        private bool isEyeFadingOut = false;

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
                rotLeftEyeFromHead = Quaternion.Inverse(head.rotation) * leftEyeBall.parent.rotation;
                rotRightEyeFromHead = Quaternion.Inverse(head.rotation) * rightEyeBall.parent.rotation;
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
        /// Enable look target tracking
        /// </summary>
        public void Enable()
        {
            enableHeadControl = true;
            enableEyeControl = true;
        }

        /// <summary>
        /// Disable look target tracking
        /// </summary>
        public void Disable()
        {
            enableHeadControl = false;
            enableEyeControl = false;
        }
    }
#endif
}
