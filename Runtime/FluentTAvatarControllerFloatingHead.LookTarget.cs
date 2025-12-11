using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Look Target control partial class
    /// Handles look target setup and real-time control
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        [SerializeField] private bool enableHeadControl = true;
        [SerializeField] [Range(0f, 20f)] private float headSpeed = 5f;

        [SerializeField] private bool enableEyeControl = true;
        [SerializeField] [Range(0f, 20f)] private float eyeSpeed = 10f;

        // Eye control strategy
        [SerializeField] private EEyeControlStrategy eyeControlStrategy = EEyeControlStrategy.Transform;
        [SerializeField] private EyeBlendShapes eyeBlendShapes = new();
        [SerializeField] [Range(0f, 45f)] private float eyeAngleLimit = 10f;
        [SerializeField] [Range(0f, 15f)] private float eyeAngleLimitThreshold = 5f;

        [Header("Virtual Target References (Set by Editor)")]
        [SerializeField] private Transform headVirtualTargetRef;
        [SerializeField] private Transform eyeVirtualTargetRef; // For Transform mode
        [SerializeField] private Transform leftEyeVirtualTargetRef; // For TransformCorrected mode
        [SerializeField] private Transform rightEyeVirtualTargetRef; // For TransformCorrected mode

        [Header("Gizmo Visualization")]
        [SerializeField] private bool showTargetGizmos = true;
        [SerializeField] private float actualTargetGizmoSize = 0.15f;
        [SerializeField] private float headVirtualTargetGizmoSize = 0.12f;
        [SerializeField] private float eyeVirtualTargetGizmoSize = 0.08f;
        [SerializeField] private Color actualTargetColor = Color.green;
        [SerializeField] private Color headVirtualTargetColor = Color.red;
        [SerializeField] private Color eyeVirtualTargetColor = Color.blue;

        // Look target controller
        private LookTargetController lookTargetController;

        #region Look Target Initialization

        private void InitializeLookTarget()
        {
            // Control TargetTracking GameObject based on enableLookTarget
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                targetTracking.gameObject.SetActive(enableLookTarget);
            }

            if (!enableLookTarget)
                return;

            // Auto-find look target transforms if not set
            if (lookHead == null || lookLeftEyeBall == null || lookRightEyeBall == null)
            {
                FindLookTargetTransforms();
            }

            // Control HeadTracking and EyeTracking GameObjects based on enable flags
            UpdateTrackingGameObjectStates();

            // Validate Multi-Aim Constraints
            if (headAimConstraint == null)
            {
                Debug.LogError("[FluentTAvatarControllerFloatingHead] Head Multi-Aim Constraint not assigned! Please assign it in the Inspector.");
                return;
            }

            if (enableEyeControl && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt &&
                (leftEyeAimConstraint == null || rightEyeAimConstraint == null))
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye control enabled but Left/Right Eye Multi-Aim Constraints not assigned!");
            }

            // Initialize LookTargetController
            lookTargetController = new LookTargetController();

            // Configure LookTargetController
            lookTargetController.SetHeadAimConstraint(headAimConstraint);
            lookTargetController.SetLeftEyeAimConstraint(leftEyeAimConstraint);
            lookTargetController.SetRightEyeAimConstraint(rightEyeAimConstraint);
            lookTargetController.SetTransforms(lookHead, lookLeftEyeBall, lookRightEyeBall);
            lookTargetController.SetLookTarget(lookTarget);

            // Set control settings
            lookTargetController.enableHeadControl = enableHeadControl;
            lookTargetController.enableEyeControl = enableEyeControl;
            lookTargetController.headSpeed = headSpeed;
            lookTargetController.eyeSpeed = eyeSpeed;

            // Set virtual target references if available (set by Editor during setup)
            if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: use separate left/right eye targets
                if (headVirtualTargetRef != null || leftEyeVirtualTargetRef != null || rightEyeVirtualTargetRef != null)
                {
                    lookTargetController.SetVirtualTargetsCorrected(headVirtualTargetRef, leftEyeVirtualTargetRef, rightEyeVirtualTargetRef);
                    lookTargetController.Initialize(useFindMethod: false);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Using serialized virtual target references for TransformCorrected mode (optimized)");
                }
                else
                {
                    // Fallback to GameObject.Find if references not set
                    lookTargetController.Initialize(useFindMethod: true);
                    Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Virtual target references not set, using GameObject.Find (slower)");
                }
            }
            else
            {
                // Transform/BlendShape mode: use single eye target
                if (headVirtualTargetRef != null || eyeVirtualTargetRef != null)
                {
                    lookTargetController.SetVirtualTargets(headVirtualTargetRef, eyeVirtualTargetRef);
                    lookTargetController.Initialize(useFindMethod: false);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Using serialized virtual target references (optimized)");
                }
                else
                {
                    // Fallback to GameObject.Find if references not set
                    lookTargetController.Initialize(useFindMethod: true);
                    Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Virtual target references not set, using GameObject.Find (slower)");
                }
            }

            // Enable
            lookTargetController.Enable();

            Debug.Log("[FluentTAvatarControllerFloatingHead] Look target initialized");
        }

        /// <summary>
        /// Auto-find look target transforms from Animator's Avatar
        /// </summary>
        public void FindLookTargetTransforms()
        {
            // Find animator if not already set
            Animator targetAnimator = animator;
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            if (targetAnimator == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] No Animator component found");
                return;
            }

            if (targetAnimator.avatar != null && targetAnimator.avatar.isHuman)
            {
                // Get transforms from HumanBodyBones
                if (lookHead == null)
                {
                    lookHead = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
                }


                if (lookLeftEyeBall == null)
                {
                    lookLeftEyeBall = targetAnimator.GetBoneTransform(HumanBodyBones.LeftEye);
                }

                if (lookRightEyeBall == null)
                {
                    lookRightEyeBall = targetAnimator.GetBoneTransform(HumanBodyBones.RightEye);
                }

                // Fallback: Use head transform if eyes not found
                bool eyesFallbackToHead = false;
                if (lookLeftEyeBall == null && lookHead != null)
                {
                    lookLeftEyeBall = lookHead;
                    eyesFallbackToHead = true;
                    Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Left eye bone not found! Using head transform as fallback.");
                }

                if (lookRightEyeBall == null && lookHead != null)
                {
                    lookRightEyeBall = lookHead;
                    eyesFallbackToHead = true;
                    Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Right eye bone not found! Using head transform as fallback.");
                }

                // Check if eye control can be enabled based on strategy
                if (eyeControlStrategy == EEyeControlStrategy.Transform || eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    // Transform strategies require actual eye ball transforms
                    if (lookLeftEyeBall == null || lookRightEyeBall == null)
                    {
                        enableEyeControl = false;
                        Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye control disabled: Transform strategy requires both eye transforms");
                    }
                    else if (eyesFallbackToHead)
                    {
                        enableEyeControl = false;
                        Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye control disabled: Transform strategy cannot use head as eye fallback");
                    }
                }
                else if (eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
                {
                    // BlendWeight strategy requires all 8 eye look blend shapes to have at least 1 entry each
                    bool hasAllBlendShapes =
                        eyeBlendShapes.eyeLookUpLeftIdx != null && eyeBlendShapes.eyeLookUpLeftIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookDownLeftIdx != null && eyeBlendShapes.eyeLookDownLeftIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookInLeftIdx != null && eyeBlendShapes.eyeLookInLeftIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookOutLeftIdx != null && eyeBlendShapes.eyeLookOutLeftIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookUpRightIdx != null && eyeBlendShapes.eyeLookUpRightIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookDownRightIdx != null && eyeBlendShapes.eyeLookDownRightIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookInRightIdx != null && eyeBlendShapes.eyeLookInRightIdx.Count > 0 &&
                        eyeBlendShapes.eyeLookOutRightIdx != null && eyeBlendShapes.eyeLookOutRightIdx.Count > 0;

                    if (!hasAllBlendShapes)
                    {
                        enableEyeControl = false;
                        Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye control disabled: BlendWeight strategy requires all 8 eye look blend shapes");
                    }
                    // BlendWeight can work with head fallback, just warn the user
                    else if (eyesFallbackToHead)
                    {
                        Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye bones not found, using head for direction calculation. BlendShape control will still work.");
                    }
                }

                Debug.Log($"[FluentTAvatarControllerFloatingHead] Found transforms - Head: {(lookHead != null ? lookHead.name : "not found")}, " +
                         $"Left Eye: {(lookLeftEyeBall != null ? lookLeftEyeBall.name : "not found")}, " +
                         $"Right Eye: {(lookRightEyeBall != null ? lookRightEyeBall.name : "not found")}");
            }
            else
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Avatar is not Humanoid type. Please use a Humanoid Avatar for auto-find feature.");
            }
        }

        /// <summary>
        /// Update tracking GameObject states based on enable flags
        /// </summary>
        private void UpdateTrackingGameObjectStates()
        {
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking == null)
                return;

            // Control HeadTracking based on enableHeadControl
            Transform headTracking = targetTracking.Find("HeadTracking");
            if (headTracking != null)
            {
                headTracking.gameObject.SetActive(enableHeadControl);
            }

            // Control LeftEyeTracking and RightEyeTracking based on enableEyeControl and strategy
            Transform leftEyeTracking = targetTracking.Find("LeftEyeTracking");
            Transform rightEyeTracking = targetTracking.Find("RightEyeTracking");

            // BlendWeightFluentt doesn't need eye tracking GameObjects
            bool needEyeTracking = enableEyeControl && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt;

            if (leftEyeTracking != null)
            {
                leftEyeTracking.gameObject.SetActive(needEyeTracking);
            }

            if (rightEyeTracking != null)
            {
                rightEyeTracking.gameObject.SetActive(needEyeTracking);
            }
        }

        #endregion

        #region Look Target Control

        private void UpdateLookTarget()
        {
            if (!Application.isPlaying)
                return;

            // Control TargetTracking GameObject based on enableLookTarget
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                targetTracking.gameObject.SetActive(enableLookTarget);
            }

            // Update HeadTracking and EyeTracking states based on enable flags
            UpdateTrackingGameObjectStates();

            if (lookTargetController == null)
                return;

            // Update look target (allows inspector changes to take effect immediately)
            lookTargetController.SetLookTarget(lookTarget);

            // Update settings (using idle settings as default)
            lookTargetController.SetLookTargetSetting(idleLookSettings);

            // Update settings
            lookTargetController.enableHeadControl = enableHeadControl;
            lookTargetController.enableEyeControl = enableEyeControl;
            lookTargetController.headSpeed = headSpeed;
            lookTargetController.eyeSpeed = eyeSpeed;

            // Update eye control strategy and BlendShape settings
            lookTargetController.eyeControlStrategy = eyeControlStrategy;
            lookTargetController.eyeBlendShapes = eyeBlendShapes;
            lookTargetController.eyeAngleLimit = eyeAngleLimit;
            lookTargetController.eyeAngleLimitThreshold = eyeAngleLimitThreshold;

            // Update virtual targets every frame
            lookTargetController.Update(Time.deltaTime);
        }

        private void LateUpdateLookTarget()
        {
            if (!Application.isPlaying || lookTargetController == null)
                return;

            // LateUpdate for BlendShape strategy
            lookTargetController.LateUpdate(Time.deltaTime);
        }

        /// <summary>
        /// Set the look target transform
        /// </summary>
        public void SetLookTarget(Transform target)
        {
            lookTarget = target;
            if (lookTargetController != null)
            {
                lookTargetController.SetLookTarget(target);
            }
        }

        /// <summary>
        /// Enable or disable look target functionality
        /// </summary>
        public void SetLookTargetEnabled(bool enabled)
        {
            enableLookTarget = enabled;

            // Control TargetTracking GameObject
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                targetTracking.gameObject.SetActive(enabled);
            }

            if (lookTargetController != null)
            {
                if (enabled)
                {
                    lookTargetController.Enable();
                    UpdateTrackingGameObjectStates();
                }
                else
                {
                    lookTargetController.Disable();
                }
            }
        }

        /// <summary>
        /// Clean up virtual targets when avatar is destroyed
        /// </summary>
        private void CleanupVirtualTargets()
        {
            // Delete avatar-specific virtual target group from VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer != null)
            {
                string avatarGroupName = $"{gameObject.name}_VirtualTargets";
                Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
                if (avatarVirtualTargetGroup != null)
                {
                    Destroy(avatarVirtualTargetGroup.gameObject);
                    Debug.Log($"[FluentTAvatarControllerFloatingHead] Deleted {avatarGroupName} group at runtime");
                }
            }
        }

        #endregion

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

            // Draw virtual targets if look target controller is initialized
            if (lookTargetController != null)
            {
                // Draw head virtual target
                if (lookTargetController.HeadVirtualTarget != null)
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

                // Draw eye virtual targets
                if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
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
