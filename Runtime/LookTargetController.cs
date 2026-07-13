using System;
using System.Collections.Generic;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Simple look target controller without timeline dependency
    /// Manages virtual targets and constraints for smooth look-at behavior
    /// Supports both Transform and BlendShape eye control strategies
    /// </summary>
    [System.Serializable]
    public partial class LookTargetController
    {
        // Virtual-target hierarchy names. The container is a leaf child of the avatar root (never of a
        // bone), so every lookup is scoped to the avatar's own subtree.
        // The names deliberately carry no bone token ("Head", "LeftEye", ...): TMUtil's bone lookup falls
        // back to a case-insensitive Contains() match, and a target named "HeadVirtualTarget" sitting in
        // the avatar subtree can shadow the real "Head" bone and receive its animation curves.
        public const string VirtualTargetsContainerName = "FT_VirtualTargets";
        public const string HeadAnchorName = "FT_LookAnchor_H";
        public const string EyeAnchorName = "FT_LookAnchor_E";
        public const string LeftEyeAnchorName = "FT_LookAnchor_L";
        public const string RightEyeAnchorName = "FT_LookAnchor_R";

        // Actual look target
        private Transform target;

        // Virtual targets for smooth tracking (Slerp-based)
        private Transform headVirtualTarget;
        private Transform eyeVirtualTarget; // Single target for both eyes (Transform mode)
        private Transform leftEyeVirtualTarget; // Left eye target (TransformCorrected mode)
        private Transform rightEyeVirtualTarget; // Right eye target (TransformCorrected mode)
        private float minDistance = 0.5f; // Minimum distance to prevent cross-eye
        private float minDistanceSqr; // Cached squared value for performance

        // Smoothed world position of each virtual target, held here instead of being re-read from the
        // target's own Transform every frame. The targets are parented under the avatar, so re-reading
        // their world position would let avatar movement drag the smoothing state and lag the gaze while
        // the avatar turns. Mirrors the eye solver's smoothedDir fields in
        // FluentTAvatarControllerFloatingHead.
        // Seeded from the target's current position on first use, and re-seeded whenever a target is
        // (re)assigned.
        private Vector3 headVtSmoothed;
        private bool headVtSeeded;
        private Vector3 eyeVtSmoothed;
        private bool eyeVtSeeded;
        private Vector3 leftEyeVtSmoothed;
        private bool leftEyeVtSeeded;
        private Vector3 rightEyeVtSmoothed;
        private bool rightEyeVtSeeded;

        // Avatar root transform (set externally for reliable hierarchy lookup)
        private Transform avatarRoot;

        // Base transforms (head and eyes)
        private Transform head;
        private Transform leftEyeBall;
        private Transform rightEyeBall;

        // Control settings
        public bool enableHeadControl = true;
        public bool enableEyeControl = false;
        public float headSpeed = 5f;
        public float eyeSpeed = 10f;

        // Set by the owner every frame: true when the rest-calibrated direct-drive solver owns that bone.
        // Such a bone's MultiAimConstraint sits at weight 0 and the solver aims at the real look target,
        // so nothing reads its virtual target — skip the per-frame Lerp + Transform write (dead work).
        // BlendWeightFluentt is never direct-driven; it still reads eyeVirtualTarget for its direction calc.
        public bool headDirectDriven;
        public bool eyeDirectDriven;

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
        /// Set avatar root transform for reliable hierarchy lookup.
        /// When set, FindVirtualTargets() uses this instead of parent traversal.
        /// </summary>
        public void SetAvatarRoot(Transform root)
        {
            avatarRoot = root;
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
            headVtSeeded = false;
            eyeVtSeeded = false;
        }

        /// <summary>
        /// Set virtual targets directly for TransformCorrected mode - separate left/right eye targets
        /// </summary>
        public void SetVirtualTargetsCorrected(Transform head, Transform leftEye, Transform rightEye)
        {
            headVirtualTarget = head;
            leftEyeVirtualTarget = leftEye;
            rightEyeVirtualTarget = rightEye;
            headVtSeeded = false;
            leftEyeVtSeeded = false;
            rightEyeVtSeeded = false;
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
        /// Find virtual targets from the container under this avatar's own root.
        /// </summary>
        private void FindVirtualTargets()
        {
            // Use externally-set avatarRoot first (most reliable)
            Transform root = avatarRoot;


            // Fallback: walk up from head bone to find Animator root
            if (root == null && head != null)
            {
                var animator = head.GetComponentInParent<Animator>();
                if (animator != null)
                    root = animator.transform;
            }

            if (root == null)
            {
                Debug.LogWarning("[LookTargetController] Cannot find avatar root!");
                return;
            }

            // Scoped to this avatar's subtree. GameObject.Find() must not be used here: it is a
            // scene-wide, active-only name search with an undefined match order, so with a container
            // per avatar it could silently return another avatar's targets.
            Transform group = root.Find(VirtualTargetsContainerName);
            if (group == null)
            {
                Debug.LogWarning($"[LookTargetController] {VirtualTargetsContainerName} not found under {root.name}!");
                return;
            }

            // Find head virtual target
            if (headVirtualTarget == null)
            {
                headVirtualTarget = group.Find(HeadAnchorName);
                headVtSeeded = false;
            }

            // Find eye virtual target (single target for both eyes - Transform mode)
            if (eyeVirtualTarget == null)
            {
                eyeVirtualTarget = group.Find(EyeAnchorName);
                eyeVtSeeded = false;
            }

            // Find left/right eye virtual targets (TransformCorrected mode)
            if (leftEyeVirtualTarget == null)
            {
                leftEyeVirtualTarget = group.Find(LeftEyeAnchorName);
                leftEyeVtSeeded = false;
            }

            if (rightEyeVirtualTarget == null)
            {
                rightEyeVirtualTarget = group.Find(RightEyeAnchorName);
                rightEyeVtSeeded = false;
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

            // Update head virtual target when enabled (skipped when the head is direct-driven —
            // its constraint sits at weight 0, so the virtual target feeds nothing)
            if (enableHeadControl && !headDirectDriven)
            {
                UpdateHeadVirtualTarget(deltaTime);
            }

            // BlendWeight strategy: always run transition detection for smooth fade-in/fade-out
            if (eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                InitializeBlendShapeValues();
            }

            // Update eye virtual targets when enabled (skipped when the eyes are direct-driven — the
            // solver aims at the real target. BlendWeightFluentt is never direct-driven and still needs
            // its virtual target for the direction calculation below.)
            if (enableEyeControl && !eyeDirectDriven)
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

            if (!headVtSeeded)
            {
                headVtSmoothed = headVirtualTarget.position;
                headVtSeeded = true;
            }

            headVtSmoothed = Vector3.Lerp(headVtSmoothed, targetPos, Mathf.Clamp01(headSpeed * deltaTime));
            headVirtualTarget.position = headVtSmoothed;
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
}
