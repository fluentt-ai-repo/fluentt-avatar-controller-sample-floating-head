using UnityEngine;
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
using UnityEngine.Animations.Rigging;
#endif

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

        // Virtual Target References (Set by Editor)
        [SerializeField] private Transform headVirtualTargetRef;
        [SerializeField] private Transform eyeVirtualTargetRef; // For Transform mode
        [SerializeField] private Transform leftEyeVirtualTargetRef; // For TransformCorrected mode
        [SerializeField] private Transform rightEyeVirtualTargetRef; // For TransformCorrected mode

        // Gizmo Visualization
        [SerializeField] private bool showTargetGizmos = true;
        [SerializeField] private float actualTargetGizmoSize = 0.15f;
        [SerializeField] private float headVirtualTargetGizmoSize = 0.12f;
        [SerializeField] private float eyeVirtualTargetGizmoSize = 0.08f;
        [SerializeField] private Color actualTargetColor = Color.green;
        [SerializeField] private Color headVirtualTargetColor = Color.red;
        [SerializeField] private Color eyeVirtualTargetColor = Color.blue;

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
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

            // Validate Multi-Aim Constraints (only required when head control is enabled)
            if (enableHeadControl && headAimConstraint == null)
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
            lookTargetController.SetAvatarRoot(transform);
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

            // Auto-find virtual target refs from hierarchy if not serialized
            TryAutoFindVirtualTargetRefs();

            // Set virtual target references if available (set by Editor, runtime setup, or auto-found above)
            if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: use separate left/right eye targets
                if (headVirtualTargetRef != null || leftEyeVirtualTargetRef != null || rightEyeVirtualTargetRef != null)
                {
                    lookTargetController.SetVirtualTargetsCorrected(headVirtualTargetRef, leftEyeVirtualTargetRef, rightEyeVirtualTargetRef);
                    lookTargetController.Initialize(useFindMethod: false);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Using virtual target references for TransformCorrected mode (optimized)");
                }
                else
                {
                    // Fallback to GameObject.Find via LookTargetController
                    lookTargetController.Initialize(useFindMethod: true);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Virtual target references not found, using GameObject.Find fallback");
                }
            }
            else
            {
                // Transform/BlendShape mode: use single eye target
                if (headVirtualTargetRef != null || eyeVirtualTargetRef != null)
                {
                    lookTargetController.SetVirtualTargets(headVirtualTargetRef, eyeVirtualTargetRef);
                    lookTargetController.Initialize(useFindMethod: false);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Using virtual target references (optimized)");
                }
                else
                {
                    // Fallback to GameObject.Find via LookTargetController
                    lookTargetController.Initialize(useFindMethod: true);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Virtual target references not found, using GameObject.Find fallback");
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
        /// Note: We keep tracking GameObjects active for smooth weight transition.
        /// Only disable when the feature itself (enableLookTarget) is disabled.
        /// Weight transition handles the actual enable/disable smoothly via constraint weights.
        /// </summary>
        private void UpdateTrackingGameObjectStates()
        {
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking == null)
                return;

            // HeadTracking is always active when Look Target is enabled
            // Weight transition handles smooth enable/disable via constraint weight
            Transform headTracking = targetTracking.Find("HeadTracking");
            if (headTracking != null)
            {
                headTracking.gameObject.SetActive(true);
            }

            // Control LeftEyeTracking and RightEyeTracking based on strategy only
            // BlendWeightFluentt doesn't need eye tracking GameObjects at all
            Transform leftEyeTracking = targetTracking.Find("LeftEyeTracking");
            Transform rightEyeTracking = targetTracking.Find("RightEyeTracking");

            bool useTransformEyeTracking = eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt;

            if (leftEyeTracking != null)
            {
                leftEyeTracking.gameObject.SetActive(useTransformEyeTracking);
            }

            if (rightEyeTracking != null)
            {
                rightEyeTracking.gameObject.SetActive(useTransformEyeTracking);
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

            // Update settings — use effective state so suppression flags are respected
            lookTargetController.enableHeadControl = enableHeadControl;
            lookTargetController.enableEyeControl = IsEyeControlEffectivelyEnabled;
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

        #region Runtime Rig Setup/Destroy

        /// <summary>
        /// Runtime version of SetupLookTargetRig.
        /// Creates Animation Rigging hierarchy (RigBuilder, Rig, MultiAimConstraints, VirtualTargets) at runtime
        /// and calls RigBuilder.Build() to activate.
        /// </summary>
        public void SetupLookTargetRigAtRuntime()
        {
            Debug.Log("[FluentTAvatarControllerFloatingHead] Setting up look target rig at runtime...");

            var avatar = gameObject;

            // 1. Ensure RigBuilder exists
            var rigBuilder = avatar.GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = avatar.AddComponent<RigBuilder>();
                Debug.Log("[FluentTAvatarControllerFloatingHead] Added RigBuilder component");
            }

            // 2. Find or create TargetTracking
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking == null)
            {
                var targetTrackingGO = new GameObject("TargetTracking");
                targetTracking = targetTrackingGO.transform;
                targetTracking.SetParent(transform);
                targetTracking.localPosition = Vector3.zero;
                targetTracking.localRotation = Quaternion.identity;
                targetTracking.localScale = Vector3.one;
                Debug.Log("[FluentTAvatarControllerFloatingHead] Created TargetTracking GameObject");
            }
            targetTracking.gameObject.SetActive(true);

            // Add Rig component
            var rig = targetTracking.GetComponent<Rig>();
            if (rig == null)
            {
                rig = targetTracking.gameObject.AddComponent<Rig>();
                Debug.Log("[FluentTAvatarControllerFloatingHead] Added Rig component to TargetTracking");
            }
            rig.weight = 1f;

            // Register Rig in RigBuilder layers
            bool rigFound = false;
            for (int i = 0; i < rigBuilder.layers.Count; i++)
            {
                if (rigBuilder.layers[i].rig == rig)
                {
                    rigFound = true;
                    break;
                }
            }
            if (!rigFound)
            {
                rigBuilder.layers.Add(new RigLayer(rig));
                Debug.Log("[FluentTAvatarControllerFloatingHead] Added Rig to RigBuilder layers");
            }

            // 3. Auto-find bone transforms
            FindLookTargetTransforms();

            // 4. Find or create avatar virtual target group
            Transform avatarVirtualTargetGroup = RuntimeFindOrCreateAvatarVirtualTargetGroup();

            // 5. Setup HeadTracking + MultiAimConstraint
            if (enableHeadControl && lookHead != null)
            {
                Transform headTracking = targetTracking.Find("HeadTracking");
                if (headTracking == null)
                {
                    var headTrackingGO = new GameObject("HeadTracking");
                    headTracking = headTrackingGO.transform;
                    headTracking.SetParent(targetTracking);
                    headTracking.localPosition = Vector3.zero;
                    headTracking.localRotation = Quaternion.identity;
                    headTracking.localScale = Vector3.one;
                }

                var headConstraint = headTracking.GetComponent<MultiAimConstraint>();
                if (headConstraint == null)
                {
                    headConstraint = headTracking.gameObject.AddComponent<MultiAimConstraint>();
                    headConstraint.weight = 1f;
                    var data = headConstraint.data;
                    data.constrainedObject = lookHead;
                    data.aimAxis = MultiAimConstraintData.Axis.Z;
                    data.upAxis = MultiAimConstraintData.Axis.Y;
                    data.limits = new Vector2(-45f, 45f);
                    // Runtime AddComponent does NOT call Reset()/SetDefaultValues(),
                    // so constrainedAxes defaults to (false,false,false) instead of (true,true,true).
                    // Without this, axesMask=(0,0,0) and no rotation is applied.
                    data.constrainedXAxis = true;
                    data.constrainedYAxis = true;
                    data.constrainedZAxis = true;
                    headConstraint.data = data;
                }

                // Create HeadVirtualTarget
                Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
                if (headVirtualTarget == null)
                {
                    var vtGO = new GameObject("HeadVirtualTarget");
                    headVirtualTarget = vtGO.transform;
                    headVirtualTarget.SetParent(avatarVirtualTargetGroup);
                    headVirtualTarget.position = lookHead.position + lookHead.forward * 2f;
                }

                // Add to constraint source objects
                var constraintData = headConstraint.data;
                var sourceObjects = constraintData.sourceObjects;
                sourceObjects.Clear();
                sourceObjects.Add(new WeightedTransform(headVirtualTarget, 1f));
                constraintData.sourceObjects = sourceObjects;
                headConstraint.data = constraintData;

                headAimConstraint = headConstraint;
                headVirtualTargetRef = headVirtualTarget;
                Debug.Log("[FluentTAvatarControllerFloatingHead] Head tracking setup complete");
            }

            // 6. Setup Eye Tracking
            if (enableEyeControl && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt)
            {
                if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    // Separate left/right eye virtual targets
                    SetupSingleEyeTrackingAtRuntime(targetTracking, avatarVirtualTargetGroup,
                        "LeftEyeTracking", lookLeftEyeBall, "LeftEyeVirtualTarget", ref leftEyeAimConstraint, ref leftEyeVirtualTargetRef);
                    SetupSingleEyeTrackingAtRuntime(targetTracking, avatarVirtualTargetGroup,
                        "RightEyeTracking", lookRightEyeBall, "RightEyeVirtualTarget", ref rightEyeAimConstraint, ref rightEyeVirtualTargetRef);
                }
                else // Transform mode — shared eye virtual target
                {
                    // Create shared EyeVirtualTarget
                    Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
                    if (eyeVirtualTarget == null)
                    {
                        var vtGO = new GameObject("EyeVirtualTarget");
                        eyeVirtualTarget = vtGO.transform;
                        eyeVirtualTarget.SetParent(avatarVirtualTargetGroup);
                        if (lookLeftEyeBall != null && lookRightEyeBall != null && lookHead != null)
                        {
                            Vector3 eyeCenter = (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f;
                            eyeVirtualTarget.position = eyeCenter + lookHead.forward * 2f;
                        }
                        else
                        {
                            eyeVirtualTarget.position = new Vector3(0, 0, 2);
                        }
                    }
                    eyeVirtualTargetRef = eyeVirtualTarget;

                    SetupSingleEyeTrackingAtRuntime(targetTracking, avatarVirtualTargetGroup,
                        "LeftEyeTracking", lookLeftEyeBall, null, ref leftEyeAimConstraint, ref leftEyeVirtualTargetRef, eyeVirtualTarget);
                    SetupSingleEyeTrackingAtRuntime(targetTracking, avatarVirtualTargetGroup,
                        "RightEyeTracking", lookRightEyeBall, null, ref rightEyeAimConstraint, ref rightEyeVirtualTargetRef, eyeVirtualTarget);
                }
            }
            else if (enableEyeControl && eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                // BlendWeightFluentt: only create eye virtual target for direction calculation
                Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
                if (eyeVirtualTarget == null)
                {
                    var vtGO = new GameObject("EyeVirtualTarget");
                    eyeVirtualTarget = vtGO.transform;
                    eyeVirtualTarget.SetParent(avatarVirtualTargetGroup);
                    if (lookLeftEyeBall != null && lookRightEyeBall != null && lookHead != null)
                    {
                        Vector3 eyeCenter = (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f;
                        eyeVirtualTarget.position = eyeCenter + lookHead.forward * 2f;
                    }
                    else
                    {
                        eyeVirtualTarget.position = new Vector3(0, 0, 2);
                    }
                }
                eyeVirtualTargetRef = eyeVirtualTarget;
            }

            // 7. Build the rig
            bool buildResult = rigBuilder.Build();
            Debug.Log($"[FluentTAvatarControllerFloatingHead] RigBuilder.Build() = {buildResult}, layers: {rigBuilder.layers.Count}");

            if (!buildResult)
            {
                Debug.LogError("[FluentTAvatarControllerFloatingHead] RigBuilder.Build() returned false!");
            }

            // 8. Initialize LookTarget controller
            enableLookTarget = true;
            InitializeLookTarget();

            Debug.Log("[FluentTAvatarControllerFloatingHead] Runtime rig setup complete!");
        }

        /// <summary>
        /// Helper: setup a single eye tracking constraint at runtime
        /// </summary>
        private void SetupSingleEyeTrackingAtRuntime(
            Transform targetTracking, Transform avatarVirtualTargetGroup,
            string trackingName, Transform eyeBoneTransform, string virtualTargetName,
            ref MultiAimConstraint aimConstraintField, ref Transform virtualTargetRefField,
            Transform sharedVirtualTarget = null)
        {
            if (eyeBoneTransform == null)
                return;

            Transform eyeTracking = targetTracking.Find(trackingName);
            if (eyeTracking == null)
            {
                var eyeTrackingGO = new GameObject(trackingName);
                eyeTracking = eyeTrackingGO.transform;
                eyeTracking.SetParent(targetTracking);
                eyeTracking.localPosition = Vector3.zero;
                eyeTracking.localRotation = Quaternion.identity;
                eyeTracking.localScale = Vector3.one;
            }

            var eyeConstraint = eyeTracking.GetComponent<MultiAimConstraint>();
            if (eyeConstraint == null)
            {
                eyeConstraint = eyeTracking.gameObject.AddComponent<MultiAimConstraint>();
                eyeConstraint.weight = 1f;
                var data = eyeConstraint.data;
                data.constrainedObject = eyeBoneTransform;
                data.aimAxis = MultiAimConstraintData.Axis.Z;
                data.upAxis = MultiAimConstraintData.Axis.Y;
                data.limits = new Vector2(-20f, 20f);
                // Runtime AddComponent does NOT call Reset()/SetDefaultValues(),
                // so constrainedAxes defaults to (false,false,false) instead of (true,true,true).
                data.constrainedXAxis = true;
                data.constrainedYAxis = true;
                data.constrainedZAxis = true;
                eyeConstraint.data = data;
            }

            // Determine which virtual target to use
            Transform targetVT = sharedVirtualTarget;
            if (targetVT == null && virtualTargetName != null)
            {
                targetVT = avatarVirtualTargetGroup.Find(virtualTargetName);
                if (targetVT == null)
                {
                    var vtGO = new GameObject(virtualTargetName);
                    targetVT = vtGO.transform;
                    targetVT.SetParent(avatarVirtualTargetGroup);
                    targetVT.position = eyeBoneTransform.position + (lookHead != null ? lookHead.forward : Vector3.forward) * 2f;
                }
                virtualTargetRefField = targetVT;
            }

            // Add to constraint source objects
            var constraintData = eyeConstraint.data;
            var sourceObjects = constraintData.sourceObjects;
            sourceObjects.Clear();
            sourceObjects.Add(new WeightedTransform(targetVT, 1f));
            constraintData.sourceObjects = sourceObjects;
            eyeConstraint.data = constraintData;

            aimConstraintField = eyeConstraint;
            Debug.Log($"[FluentTAvatarControllerFloatingHead] {trackingName} setup complete");
        }

        /// <summary>
        /// Destroy all runtime-created rig objects and rebuild with empty state.
        /// </summary>
        public void DestroyLookTargetRigAtRuntime()
        {
            Debug.Log("[FluentTAvatarControllerFloatingHead] Destroying look target rig at runtime...");

            // 1. Disable and clear LookTargetController
            if (lookTargetController != null)
            {
                lookTargetController.Disable();
                lookTargetController = null;
            }
            enableLookTarget = false;

            // 2. Clear RigBuilder — tears down the PlayableGraph.
            var rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder != null)
            {
                rigBuilder.Clear();
                rigBuilder.layers.Clear();
                Debug.Log("[FluentTAvatarControllerFloatingHead] RigBuilder cleared");
            }

            // 3. Strip constraint/rig components from TargetTracking, but keep the GameObjects.
            //    RigBuilder.Build() registers PropertyStreamHandle bindings in the Animator's
            //    internal native cache (e.g. path "TargetTracking/HeadTracking").
            //    These bindings persist even after Clear()/Rebind()/controller reassignment.
            //    If the GameObjects are destroyed, every AnimatorOverrideController.set_Item
            //    call triggers "Could not resolve" warnings indefinitely.
            //    By keeping the empty GameObjects (deactivated), the transform paths remain
            //    resolvable and no warnings are produced.
            Transform targetTracking = transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                // Remove all constraint and rig components
                foreach (var mac in targetTracking.GetComponentsInChildren<MultiAimConstraint>(true))
                    DestroyImmediate(mac);
                var rig = targetTracking.GetComponent<Rig>();
                if (rig != null)
                    DestroyImmediate(rig);

                targetTracking.gameObject.SetActive(false);
                Debug.Log("[FluentTAvatarControllerFloatingHead] TargetTracking stripped and deactivated");
            }

            // 4. Destroy avatar virtual target group
            CleanupVirtualTargets();

            // 5. Clear serialized field references
            headAimConstraint = null;
            leftEyeAimConstraint = null;
            rightEyeAimConstraint = null;
            headVirtualTargetRef = null;
            eyeVirtualTargetRef = null;
            leftEyeVirtualTargetRef = null;
            rightEyeVirtualTargetRef = null;

            Debug.Log("[FluentTAvatarControllerFloatingHead] Runtime rig destroy complete!");
        }

        /// <summary>
        /// Manually trigger RigBuilder.Build() and log the result.
        /// </summary>
        public void RebuildRig()
        {
            var rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] No RigBuilder found on this GameObject");
                return;
            }

            int layerCount = rigBuilder.layers.Count;
            rigBuilder.Build();
            Debug.Log($"[FluentTAvatarControllerFloatingHead] RigBuilder.Build() called — {layerCount} layer(s)");
        }

        /// <summary>
        /// Try to auto-find virtual target references from existing VirtualTargets hierarchy.
        /// Called before the ref null-check in InitializeLookTarget() to avoid unnecessary GameObject.Find fallback.
        /// </summary>
        private void TryAutoFindVirtualTargetRefs()
        {
            // Skip if all potentially needed refs are already set
            bool hasHeadRef = headVirtualTargetRef != null;
            bool hasEyeRef = eyeVirtualTargetRef != null;
            bool hasCorrectedRefs = leftEyeVirtualTargetRef != null && rightEyeVirtualTargetRef != null;

            if (hasHeadRef && (eyeControlStrategy == EEyeControlStrategy.TransformCorrected ? hasCorrectedRefs : hasEyeRef))
                return;

            GameObject container = GameObject.Find("VirtualTargets");
            if (container == null)
                return;

            string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
            string groupName = $"{cleanName}_VirtualTargets";
            Transform group = container.transform.Find(groupName);
            if (group == null)
                return;

            if (headVirtualTargetRef == null)
                headVirtualTargetRef = group.Find("HeadVirtualTarget");
            if (eyeVirtualTargetRef == null)
                eyeVirtualTargetRef = group.Find("EyeVirtualTarget");
            if (leftEyeVirtualTargetRef == null)
                leftEyeVirtualTargetRef = group.Find("LeftEyeVirtualTarget");
            if (rightEyeVirtualTargetRef == null)
                rightEyeVirtualTargetRef = group.Find("RightEyeVirtualTarget");
        }

        /// <summary>
        /// Helper: Find or create the VirtualTargets container and avatar group at runtime
        /// </summary>
        private Transform RuntimeFindOrCreateAvatarVirtualTargetGroup()
        {
            GameObject container = GameObject.Find("VirtualTargets");
            if (container == null)
            {
                container = new GameObject("VirtualTargets");
                Debug.Log("[FluentTAvatarControllerFloatingHead] Created VirtualTargets container");
            }

            string cleanName = gameObject.name.Replace("(Clone)", "").Trim();
            string groupName = $"{cleanName}_VirtualTargets";
            Transform group = container.transform.Find(groupName);
            if (group == null)
            {
                var groupGO = new GameObject(groupName);
                group = groupGO.transform;
                group.SetParent(container.transform);
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Created {groupName} group");
            }
            return group;
        }

        #endregion
#else
        // Animation Rigging not installed - stub implementations
        private void InitializeLookTarget()
        {
            if (enableLookTarget)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animation Rigging package is not installed. Look Target feature is disabled. Install 'com.unity.animation.rigging' via Package Manager.");
                enableLookTarget = false;
            }
        }
        public void FindLookTargetTransforms() { }
        private void UpdateLookTarget() { }
        private void LateUpdateLookTarget() { }
        public void SetLookTarget(Transform target) { lookTarget = target; }
        public void SetLookTargetEnabled(bool enabled) { enableLookTarget = enabled; }
        private void CleanupVirtualTargets() { }
        public void SetupLookTargetRigAtRuntime()
        {
            Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animation Rigging package is not installed. Cannot setup rig at runtime.");
        }
        public void DestroyLookTargetRigAtRuntime()
        {
            Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animation Rigging package is not installed. Cannot destroy rig at runtime.");
        }
        public void RebuildRig()
        {
            Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Animation Rigging package is not installed. Cannot rebuild rig.");
        }
#endif
    }
}
