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
        // Max angle the head can rotate toward the target (applied to head MultiAimConstraint limits)
        [SerializeField] [Range(0f, 90f)] private float headAngleLimit = 45f;

        [SerializeField] private bool enableEyeControl = true;
        [SerializeField] [Range(0f, 20f)] private float eyeSpeed = 10f;

        // Eye control strategy
        [SerializeField] private EEyeControlStrategy eyeControlStrategy = EEyeControlStrategy.Transform;
        [SerializeField] private EyeBlendShapes eyeBlendShapes = new();
        // BlendShape strategy: angle cutoff (stop eye tracking beyond this angle) + hysteresis threshold
        [SerializeField] [Range(0f, 45f)] private float eyeAngleLimit = 10f;
        [SerializeField] [Range(0f, 15f)] private float eyeAngleLimitThreshold = 5f;
        // Transform/TransformCorrected strategy: max angle the eyes can rotate (applied to eye MultiAimConstraint limits)
        [SerializeField] [Range(0f, 90f)] private float eyeTransformAngleLimit = 20f;
        // Auto-detect each eye/head bone's true gaze axis at init and set the MultiAimConstraint aimAxis/upAxis
        // accordingly. Handles "twisted" rigs whose bone local +Z is not the gaze direction (otherwise horizontal
        // tracking collapses into roll about the gaze axis). Turn off to keep manually-authored constraint axes.
        [SerializeField] private bool autoDetectEyeAimAxis = true;

        // Eye-aim mode for Transform strategies. Some rigs author the eye bones with negative (mirrored)
        // scale; MultiAimConstraint cannot aim a mirrored bone (its world-up twist solve assumes a
        // right-handed basis, so horizontal tracking collapses into roll about the gaze axis). The
        // DirectUniversal solver drives the eye bones directly from a rest-calibrated LookRotation, which
        // is robust to ANY axis orientation and ANY scale incl. mirrors. Auto (default) is the same
        // direct-drive solver: it is exact for any bone axis orientation, so there is no reason to fall
        // back to the constraint. Constraint keeps the legacy path. BlendWeightFluentt is unaffected.
        [SerializeField] private EEyeAimMode eyeAimMode = EEyeAimMode.Auto;

        // Head-aim mode. DirectCalibrated (default) measures the head bone's true facial forward against
        // the avatar root at bind pose and drives the bone directly each LateUpdate — exact for rigs whose
        // head-bone local +Z is not the gaze direction (e.g. Rigify DEF-spine.005 with ~7.5deg rest tilt,
        // which makes the MultiAim path aim that many degrees above the target) — and aims from the eye
        // midpoint (no pivot-vs-eye parallax). Constraint keeps the legacy MultiAimConstraint path.
        [SerializeField] private EHeadAimMode headAimMode = EHeadAimMode.DirectCalibrated;

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

            // Capture bind-pose reference data for the direct-aim solvers before anything can animate
            // the bones (idempotent — a later re-init reuses the first capture).
            CaptureLookAimBindPose();

            // Control HeadTracking and EyeTracking GameObjects based on enable flags
            UpdateTrackingGameObjectStates();

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // Validate Multi-Aim Constraints (only required when head control uses the constraint path;
            // DirectCalibrated drives the head bone directly and needs no constraint)
            if (enableHeadControl && headAimMode == EHeadAimMode.Constraint && headAimConstraint == null)
            {
                Debug.LogError("[FluentTAvatarControllerFloatingHead] Head Multi-Aim Constraint not assigned! Please assign it in the Inspector.");
                return;
            }

            if (enableEyeControl && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt &&
                (leftEyeAimConstraint == null || rightEyeAimConstraint == null))
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] Eye control enabled but Left/Right Eye Multi-Aim Constraints not assigned!");
            }
#endif

            // Initialize LookTargetController
            lookTargetController = new LookTargetController();

            // Configure LookTargetController
            lookTargetController.SetAvatarRoot(transform);
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            lookTargetController.SetHeadAimConstraint(headAimConstraint);
            lookTargetController.SetLeftEyeAimConstraint(leftEyeAimConstraint);
            lookTargetController.SetRightEyeAimConstraint(rightEyeAimConstraint);
#endif
            lookTargetController.SetTransforms(lookHead, lookLeftEyeBall, lookRightEyeBall);
            lookTargetController.SetLookTarget(lookTarget);

            // Set control settings
            lookTargetController.enableHeadControl = enableHeadControl;
            lookTargetController.enableEyeControl = enableEyeControl;
            lookTargetController.headSpeed = headSpeed;
            lookTargetController.eyeSpeed = eyeSpeed;

            // Ensure virtual targets exist (find or auto-create for runtime Instantiate)
            EnsureVirtualTargetRefs();

            // Set virtual target references if available (set by Editor, runtime setup, or auto-found above)
            if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: use separate left/right eye targets
                if (headVirtualTargetRef != null || leftEyeVirtualTargetRef != null || rightEyeVirtualTargetRef != null)
                {
                    lookTargetController.SetVirtualTargetsCorrected(headVirtualTargetRef, leftEyeVirtualTargetRef, rightEyeVirtualTargetRef);
                    lookTargetController.Initialize(useFindMethod: false);
                    if (enableVerboseLogging) Debug.Log("[FluentTAvatarControllerFloatingHead] Using virtual target references for TransformCorrected mode (optimized)");
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
                    if (enableVerboseLogging) Debug.Log("[FluentTAvatarControllerFloatingHead] Using virtual target references (optimized)");
                }
                else
                {
                    // Fallback to GameObject.Find via LookTargetController
                    lookTargetController.Initialize(useFindMethod: true);
                    Debug.Log("[FluentTAvatarControllerFloatingHead] Virtual target references not found, using GameObject.Find fallback");
                }
            }

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // Detect each eye/head bone's real gaze axis and configure the MultiAimConstraint aim axes
            // (handles "twisted" rigs), then rebuild the rig so the new axes take effect.
            if (autoDetectEyeAimAxis)
                AutoConfigureLookAimAxes();
#endif

            // Enable
            lookTargetController.Enable();

            // Universal eye-aim: calibrate while the eye bones are still in bind pose (Start runs before the
            // first animation/rig evaluation) and disable the eye constraints. Only engages when the eye-aim
            // mode selects it (Auto/DirectUniversal).
            if (WillUseUniversalEyeAim())
                CalibrateUniversalEyeAim();

            // Head direct-aim: calibrate the head bone's true facial forward while still in bind pose and
            // silence the head constraint so the rig does not aim the (possibly tilted) bone +Z axis.
            // Per-frame gating on enableHeadControl happens in the drive itself.
            if (headAimMode == EHeadAimMode.DirectCalibrated)
                CalibrateHeadAim();

            if (enableVerboseLogging) Debug.Log("[FluentTAvatarControllerFloatingHead] Look target initialized");
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

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
        // Bone is considered "aligned" (no twist) when its local +Z is within this dot of the gaze direction.
        private const float AimAxisAlignedThreshold = 0.99f;

        /// <summary>
        /// Detect the real gaze axis of each eye (and head) bone and set the corresponding
        /// MultiAimConstraint aimAxis/upAxis. Eye/head bones can be authored with arbitrary local
        /// orientation ("twisted" rigs) — e.g. local +Z points down while the gaze is local +Y.
        /// MultiAimConstraint assumes aimAxis is the gaze axis; when it isn't, horizontal tracking
        /// collapses into roll about the gaze axis (only vertical tracking survives). Detection runs
        /// once at init (no per-frame cost) and the rig is rebuilt so the new axes take effect.
        /// </summary>
        private void AutoConfigureLookAimAxes()
        {
            // Reference gaze = avatar root forward (bind pose faces root forward). Do NOT use the head
            // bone's forward here: the head bone's local +Z can be tilted relative to the true gaze, and
            // for the head constraint itself that reference is self-referential (gazeLocal is identically
            // Vector3.forward), which made the head always pass as "aligned" and left its rest tilt
            // uncorrected.
            Vector3 gazeRef = transform.forward;
            bool changed = false;

            // Skip eye constraints when the universal direct-drive solver will own the eyes.
            if (enableEyeControl && !WillUseUniversalEyeAim() && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt)
            {
                changed |= ConfigureAimAxisForBone(leftEyeAimConstraint, lookLeftEyeBall, gazeRef);
                changed |= ConfigureAimAxisForBone(rightEyeAimConstraint, lookRightEyeBall, gazeRef);
            }

            // Head constraint axes only matter on the legacy path; DirectCalibrated zeroes its weight.
            if (enableHeadControl && headAimMode == EHeadAimMode.Constraint)
                changed |= ConfigureAimAxisForBone(headAimConstraint, lookHead, gazeRef);

            if (changed)
            {
                var rigBuilder = GetComponent<RigBuilder>();
                if (rigBuilder != null)
                {
                    rigBuilder.Build();
                    if (enableVerboseLogging) Debug.Log("[FluentTAvatarControllerFloatingHead] Rig rebuilt after auto-configuring aim axes");
                }
            }
        }

        /// <summary>
        /// Set a single MultiAimConstraint's aimAxis/upAxis to match the bone's true gaze axis.
        /// Returns true if anything changed (so the caller can rebuild the rig once).
        /// </summary>
        private bool ConfigureAimAxisForBone(MultiAimConstraint constraint, Transform bone, Vector3 desiredGazeWorld)
        {
            if (constraint == null || bone == null || desiredGazeWorld.sqrMagnitude < 1e-8f)
                return false;

            Vector3 gazeLocal = (Quaternion.Inverse(bone.rotation) * desiredGazeWorld).normalized;

            MultiAimConstraintData.Axis newAim;
            MultiAimConstraintData.Axis newUp;
            MultiAimConstraintData.WorldUpType newWorldUp;
            Vector3 newOffset;

            var data = constraint.data;

            if (Vector3.Dot(gazeLocal, Vector3.forward) >= AimAxisAlignedThreshold)
            {
                // Aligned rig: bone +Z already is the gaze axis (SDK default). Leave as-is.
                newAim = MultiAimConstraintData.Axis.Z;
                newUp = MultiAimConstraintData.Axis.Y;
                newWorldUp = data.worldUpType;
                newOffset = Vector3.zero;
            }
            else
            {
                // Twisted rig: aim the local cardinal axis nearest the gaze.
                Vector3 aimSnapped;
                newAim = NearestLocalAxis(gazeLocal, out aimSnapped);

                // Pick an up axis perpendicular to the aim axis, nearest to world up.
                Vector3 upLocal = Vector3.ProjectOnPlane(Quaternion.Inverse(bone.rotation) * Vector3.up, aimSnapped);
                if (upLocal.sqrMagnitude < 1e-6f)
                    upLocal = Vector3.ProjectOnPlane(Quaternion.Inverse(bone.rotation) * Vector3.forward, aimSnapped);
                Vector3 upSnapped;
                newUp = NearestLocalAxis(upLocal, out upSnapped);

                newWorldUp = MultiAimConstraintData.WorldUpType.SceneUp;
                // No residual offset: a well-authored eye bone's gaze lies on a cardinal local axis, so the
                // snapped aim axis already is the gaze. Deriving an offset from a gaze *proxy* (head.forward)
                // bakes in the head bone's own tilt and harms accuracy (measured: 7.5deg error vs ~1-3deg with
                // zero offset). Leave offset at zero; the snapped cardinal axis is the gaze axis.
                newOffset = Vector3.zero;
            }

            bool changed = data.aimAxis != newAim || data.upAxis != newUp ||
                           data.worldUpType != newWorldUp || data.offset != newOffset;
            if (changed)
            {
                data.aimAxis = newAim;
                data.upAxis = newUp;
                data.worldUpType = newWorldUp;
                data.offset = newOffset;
                constraint.data = data;
                if (enableVerboseLogging)
                    Debug.Log($"[FluentTAvatarControllerFloatingHead] Auto-aim {bone.name}: aimAxis={newAim} upAxis={newUp} worldUp={newWorldUp} offset={newOffset}");
            }
            return changed;
        }

        /// <summary>Returns the local cardinal axis (one of ±X/±Y/±Z) closest to v, plus its unit vector.</summary>
        private static MultiAimConstraintData.Axis NearestLocalAxis(Vector3 v, out Vector3 snapped)
        {
            v = v.normalized;
            float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
            if (ax >= ay && ax >= az)
            {
                snapped = v.x >= 0f ? Vector3.right : Vector3.left;
                return v.x >= 0f ? MultiAimConstraintData.Axis.X : MultiAimConstraintData.Axis.X_NEG;
            }
            if (ay >= az)
            {
                snapped = v.y >= 0f ? Vector3.up : Vector3.down;
                return v.y >= 0f ? MultiAimConstraintData.Axis.Y : MultiAimConstraintData.Axis.Y_NEG;
            }
            snapped = v.z >= 0f ? Vector3.forward : Vector3.back;
            return v.z >= 0f ? MultiAimConstraintData.Axis.Z : MultiAimConstraintData.Axis.Z_NEG;
        }
#endif

        // ── Universal direct eye-aim solver (handles mirrored / any-scale eye bones) ──────────────────
        // MultiAimConstraint cannot aim a mirrored (negative-scale / reflected) eye bone: its world-up
        // twist solve assumes a right-handed basis, so a reflected eye collapses horizontal tracking into
        // roll about the gaze axis. This solver instead measures each eye's gaze/up direction in its own
        // local space at the bind pose (which implicitly bakes in the bone's scale/mirror) and re-aims it
        // directly each LateUpdate — no cardinal-axis or handedness assumption, so it works for any rig.
        private bool _eyeAimCalibrated;
        private bool _lookAimBindPoseCaptured;
        private Vector3 _leftEyeLocalGaze, _leftEyeLocalUp;
        private Vector3 _rightEyeLocalGaze, _rightEyeLocalUp;

        // Smoothing state: the gaze direction each eye is currently aiming along, in WORLD space, carried
        // across frames. Smoothing the DIRECTION (not the final bone rotation) is what makes the solver
        // converge: the Animator rewrites the eye bone every frame, so a Slerp seeded from eye.rotation
        // restarts from the animated pose each frame and only ever travels a fraction t of the way — the
        // eyes then settle a fixed angle short of the target, and the error GROWS with framerate (t =
        // eyeSpeed * deltaTime shrinks). Keeping the direction in a field makes the approach accumulate.
        // World space (not head-local) so the eyes stay locked on the target while the head turns.
        private Vector3 _leftEyeSmoothedDir, _rightEyeSmoothedDir;
        private bool _eyeAimDrivingLastFrame;

        /// <summary>
        /// Capture the bind-pose reference data shared by the head and eye direct-aim solvers, exactly
        /// once. The reference gaze is the avatar root's forward (bind pose faces root forward) — NOT the
        /// head bone's forward, which can be tilted relative to the true gaze (e.g. Rigify DEF-spine.005,
        /// ~7.5deg) and would bake that tilt into the calibration. Start runs before the first animation
        /// evaluation, so the first call sees the authored bind pose; later re-inits (e.g.
        /// SetupLookTargetRigAtRuntime mid-gameplay) reuse the original capture instead of reading an
        /// animated pose.
        /// </summary>
        private void CaptureLookAimBindPose()
        {
            if (_lookAimBindPoseCaptured)
                return;

            Vector3 gaze0 = transform.forward;
            Vector3 up0 = transform.up;

            if (lookHead != null)
            {
                Quaternion inv = Quaternion.Inverse(lookHead.rotation);
                _headLocalGaze = inv * gaze0;
                _headLocalUp = inv * up0;
            }
            if (lookLeftEyeBall != null)
            {
                Quaternion inv = Quaternion.Inverse(lookLeftEyeBall.rotation);
                _leftEyeLocalGaze = inv * gaze0;
                _leftEyeLocalUp = inv * up0;
            }
            if (lookRightEyeBall != null)
            {
                Quaternion inv = Quaternion.Inverse(lookRightEyeBall.rotation);
                _rightEyeLocalGaze = inv * gaze0;
                _rightEyeLocalUp = inv * up0;
            }

            _lookAimBindPoseCaptured = lookHead != null || lookLeftEyeBall != null || lookRightEyeBall != null;
        }

        /// <summary>
        /// Whether the direct-drive universal eye-aim solver should own the eyes (vs the MultiAimConstraint
        /// path). DirectUniversal: always; Constraint: never; Auto: always (the rest-calibrated solver is
        /// exact for any bone axis/scale). BlendWeightFluentt never uses eye constraints.
        /// </summary>
        private bool WillUseUniversalEyeAim()
        {
            if (!enableEyeControl || eyeControlStrategy == EEyeControlStrategy.BlendWeightFluentt)
                return false;
            switch (eyeAimMode)
            {
                case EEyeAimMode.DirectUniversal: return true;
                case EEyeAimMode.Constraint: return false;
                // Auto: the rest-calibrated solver is exact for ANY bone axis orientation (incl.
                // non-cardinal gaze axes, e.g. VRoid eye bones ~23deg off-cardinal) and any scale incl.
                // mirrored bones, so it is the default for all rigs.
                default: return true;
            }
        }

        /// <summary>
        /// True when the direct eye solver owns the eye bones RIGHT NOW: it was calibrated at init AND the
        /// current strategy still uses it. The strategy term matters because eyeControlStrategy is pushed
        /// from the inspector every frame and can flip to BlendWeightFluentt during play, while
        /// _eyeAimCalibrated (captured once at init) cannot. Every "do the eyes belong to the solver?"
        /// decision must go through this property — a stale ownership flag would freeze the virtual target
        /// that BlendWeightFluentt reads for its direction calculation, and the eyes would stop tracking.
        /// The head's equivalent (_headAimCalibrated) is reconciled every frame by SyncHeadAimMode().
        /// </summary>
        private bool EyesDirectDriven =>
            _eyeAimCalibrated && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt;

        /// <summary>
        /// True when the bone has a mirrored/reflected (left-handed) basis — an odd number of negative scale
        /// axes anywhere in its parent chain (matrix determinant &lt; 0). MultiAimConstraint cannot aim it.
        /// </summary>
        private static bool IsEyeBoneReflected(Transform bone)
        {
            return bone != null && bone.localToWorldMatrix.determinant < 0f;
        }

        /// <summary>
        /// Capture each eye's gaze/up direction expressed in its own local space at the bind/rest pose,
        /// and disable the eye MultiAimConstraints so the rig does not fight the direct drive.
        /// Must be called while the eye bones are still in bind pose (i.e. from Start, before animation).
        /// </summary>
        private void CalibrateUniversalEyeAim()
        {
            _eyeAimCalibrated = false;

            // Reference directions are captured once at bind pose (see CaptureLookAimBindPose); re-inits
            // (e.g. SetupLookTargetRigAtRuntime mid-gameplay) reuse the original capture.
            CaptureLookAimBindPose();

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // Eyes are driven directly in LateUpdate; silence the rig constraints so the PlayableGraph
            // does not also write eye rotations (which are wrong for mirrored bones).
            if (leftEyeAimConstraint != null) leftEyeAimConstraint.weight = 0f;
            if (rightEyeAimConstraint != null) rightEyeAimConstraint.weight = 0f;
#endif

            // Force the first drive to seed its smoothed gaze from the (bind/animated) eye pose.
            _eyeAimDrivingLastFrame = false;
            _leftEyeSmoothedDir = Vector3.zero;
            _rightEyeSmoothedDir = Vector3.zero;

            _eyeAimCalibrated = _lookAimBindPoseCaptured && (lookLeftEyeBall != null || lookRightEyeBall != null);
            if (enableVerboseLogging)
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Universal eye-aim calibrated (L:{lookLeftEyeBall != null} R:{lookRightEyeBall != null})");
        }

        /// <summary>
        /// Drive both eye bones to look at the current look target, clamped to eyeTransformAngleLimit and
        /// smoothed by eyeSpeed. Called every LateUpdate, after the rig/animation and the head aim have run.
        /// </summary>
        private void DriveUniversalEyeAim()
        {
            if (!_eyeAimCalibrated || lookTarget == null)
                return;

            Vector3 aimPos = lookTarget.position;
            // Clamp/roll reference = the head's FACIAL forward/up (calibrated), not the raw bone axes,
            // which can be tilted relative to the face on some rigs.
            Vector3 headFwd = CurrentHeadGazeDirection();
            Vector3 worldUp = CurrentHeadUpDirection();
            float t = Mathf.Clamp01(eyeSpeed * Time.deltaTime);
            // Resuming after a gap (first drive, or eye control was off): start from where the animation
            // currently has the eye, so the gaze ramps in instead of snapping to a stale direction.
            bool reseed = !_eyeAimDrivingLastFrame;

            AimSingleEyeUniversal(lookLeftEyeBall, _leftEyeLocalGaze, _leftEyeLocalUp,
                ref _leftEyeSmoothedDir, reseed, aimPos, headFwd, worldUp, t);
            AimSingleEyeUniversal(lookRightEyeBall, _rightEyeLocalGaze, _rightEyeLocalUp,
                ref _rightEyeSmoothedDir, reseed, aimPos, headFwd, worldUp, t);

            _eyeAimDrivingLastFrame = true;
        }

        /// <summary>
        /// Aim one eye bone so its measured local gaze axis points at <paramref name="aimPos"/>.
        /// The (gaze, up) local frame is mapped onto the desired world (dir, up) frame via LookRotation,
        /// which is valid for any handedness/scale because the local frame was measured post-scale.
        /// Smoothing is applied to <paramref name="smoothedDir"/> — a world-space direction that persists
        /// across frames — and the bone rotation is then written outright. Slerping the bone rotation
        /// instead would restart from the Animator's freshly-written pose every frame and never converge.
        /// </summary>
        private void AimSingleEyeUniversal(Transform eye, Vector3 localGaze, Vector3 localUp,
            ref Vector3 smoothedDir, bool reseed, Vector3 aimPos, Vector3 headFwd, Vector3 worldUp, float t)
        {
            if (eye == null)
                return;

            Vector3 dir = aimPos - eye.position;
            if (dir.sqrMagnitude < 1e-10f)
                return;
            dir.Normalize();

            // Clamp gaze to within eyeTransformAngleLimit of the head forward (mirrors the constraint limit).
            float limitRad = eyeTransformAngleLimit * Mathf.Deg2Rad;
            dir = Vector3.RotateTowards(headFwd, dir, limitRad, 0f);

            if (reseed || smoothedDir.sqrMagnitude < 1e-8f)
                smoothedDir = eye.rotation * localGaze; // where the animated pose currently looks

            smoothedDir = Vector3.Slerp(smoothedDir, dir, t);
            if (smoothedDir.sqrMagnitude < 1e-8f)
                smoothedDir = dir;
            else
                smoothedDir.Normalize();

            // Re-clamp after smoothing: the head may have turned since the stored direction was set.
            Vector3 finalDir = Vector3.RotateTowards(headFwd, smoothedDir, limitRad, 0f);

            Quaternion src = Quaternion.LookRotation(localGaze, localUp);
            eye.rotation = Quaternion.LookRotation(finalDir, worldUp) * Quaternion.Inverse(src);
        }

        // ── Rest-calibrated direct head-aim solver ─────────────────────────────────────────────────────
        // The MultiAimConstraint head path assumes the head bone's local +Z is the facial forward. On many
        // rigs it is not (measured in-project: Rigify DEF-spine.005 +Z tilted 7.49deg down -> head aims
        // 7.49deg above the target; other rigs show yawed or flipped head axes). This solver measures the
        // head bone's true facial forward against the avatar root at bind pose and re-aims it directly each
        // LateUpdate, as a shortest-arc correction on top of the animated pose (so animated head motion and
        // twist survive). It also aims from the eye midpoint, removing the pivot-vs-eye parallax error.
        private bool _headAimCalibrated;
        private Vector3 _headLocalGaze = Vector3.forward;
        private Vector3 _headLocalUp = Vector3.up;
        private Quaternion _headAimCorrection = Quaternion.identity; // smoothed world-space correction
        private Quaternion _lastWrittenHeadLocalRotation = Quaternion.identity;
        private Quaternion _lastCleanHeadLocalRotation = Quaternion.identity;
        private bool _hasLastWrittenHeadRotation;

        /// <summary>
        /// Activate the direct head-aim solver: reuse the bind-pose capture (see CaptureLookAimBindPose)
        /// and silence the head MultiAimConstraint so the rig does not fight the direct drive.
        /// </summary>
        private void CalibrateHeadAim()
        {
            _headAimCalibrated = false;
            if (lookHead == null)
                return;

            // Reference directions are captured once at bind pose; safe to re-run mid-play.
            CaptureLookAimBindPose();
            ResetLookAimCorrection();
            _lastCleanHeadLocalRotation = lookHead.localRotation;

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // The head is driven directly in LateUpdate; silence the rig constraint so the PlayableGraph
            // does not also aim the (possibly tilted) bone +Z axis.
            if (headAimConstraint != null)
                headAimConstraint.weight = 0f;
#endif

            _headAimCalibrated = _lookAimBindPoseCaptured;
            if (enableVerboseLogging)
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Head aim calibrated (bone: {lookHead.name}, localGaze: {_headLocalGaze})");
        }

        /// <summary>Drop any residual head-aim correction so the next activation ramps in from identity.</summary>
        private void ResetLookAimCorrection()
        {
            _headAimCorrection = Quaternion.identity;
            _hasLastWrittenHeadRotation = false;
        }

        /// <summary>
        /// Drop ALL direct-aim smoothing state (head correction + eye smoothed gaze) so the next drive ramps
        /// in from the current animated pose. Must run whenever the solvers stop driving for a while: their
        /// state is absolute (a world-space eye direction, a world-space head correction) and goes stale as
        /// the avatar or the target keeps moving, which would pop the bones on the first driven frame.
        /// </summary>
        private void ResetLookAimSmoothing()
        {
            ResetLookAimCorrection();
            _eyeAimDrivingLastFrame = false;
            _leftEyeSmoothedDir = Vector3.zero;
            _rightEyeSmoothedDir = Vector3.zero;
        }

        /// <summary>
        /// Keep the head constraint weight and the direct solver's activation consistent with the
        /// (runtime-editable) headAimMode. Allows switching DirectCalibrated &lt;-&gt; Constraint during
        /// play: Direct zeroes the constraint and (re)activates the solver from the bind-pose capture;
        /// Constraint restores the rig weight and releases the solver.
        /// </summary>
        private void SyncHeadAimMode()
        {
            if (headAimMode == EHeadAimMode.DirectCalibrated)
            {
                if (!_headAimCalibrated && lookHead != null)
                    CalibrateHeadAim(); // reuses the bind-pose capture; safe mid-play
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
                if (headAimConstraint != null && headAimConstraint.weight != 0f)
                    headAimConstraint.weight = 0f;
#endif
            }
            else
            {
                if (_headAimCalibrated)
                {
                    _headAimCalibrated = false;
                    ResetLookAimCorrection();
                    // Hand the head back to the constraint: its virtual target has been idle while the
                    // solver owned the bone, so put it on the target to avoid a swing on the first frame.
                    if (lookTargetController != null)
                        lookTargetController.SnapHeadVirtualTargetToTarget();
                }
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
                if (headAimConstraint != null && headAimConstraint.weight != 1f)
                    headAimConstraint.weight = 1f;
#endif
            }
        }

        /// <summary>
        /// The head's current FACIAL forward (calibrated), used as the eye clamp reference. Uses the
        /// bind-pose capture (not the head-solver activation flag) so the reference stays the true facial
        /// forward even when the head runs on the legacy Constraint mode with a tilted bone axis.
        /// </summary>
        private Vector3 CurrentHeadGazeDirection()
        {
            if (lookHead == null) return transform.forward;
            return _lookAimBindPoseCaptured ? lookHead.rotation * _headLocalGaze : lookHead.forward;
        }

        /// <summary>The head's current FACIAL up (calibrated), used as the eye roll reference.</summary>
        private Vector3 CurrentHeadUpDirection()
        {
            if (lookHead == null) return Vector3.up;
            return _lookAimBindPoseCaptured ? lookHead.rotation * _headLocalUp : lookHead.up;
        }

        /// <summary>
        /// Rotate the head bone so its calibrated facial forward points at the look target, on top of the
        /// animated pose. Called every LateUpdate (after animation/rig) BEFORE the eye drive, so the eyes
        /// see the corrected head pose. Clamped to headAngleLimit and smoothed by headSpeed; when tracking
        /// is disabled the correction fades back to identity (smooth disable, mirrors constraint weight fade).
        /// </summary>
        private void DriveCalibratedHeadAim()
        {
            if (!_headAimCalibrated || lookHead == null)
                return;

            // Recover the clean animated rotation. Animation normally rewrites the head's LOCAL rotation
            // every frame; if it did not (no clip running / no head curve), localRotation still holds last
            // frame's corrected value, which must not be corrected again (compounding). The comparison is
            // done in LOCAL space so parent/root motion can never masquerade as an animation rewrite —
            // otherwise the correction compounds and silently bypasses headAngleLimit.
            Quaternion animatedLocal = lookHead.localRotation;
            bool recoveredCleanPose = _hasLastWrittenHeadRotation &&
                Quaternion.Angle(animatedLocal, _lastWrittenHeadLocalRotation) < 0.01f;
            if (recoveredCleanPose)
                animatedLocal = _lastCleanHeadLocalRotation;
            _lastCleanHeadLocalRotation = animatedLocal;
            Quaternion parentRot = lookHead.parent != null ? lookHead.parent.rotation : Quaternion.identity;
            Quaternion animatedRot = parentRot * animatedLocal;

            bool track = enableLookTarget && enableHeadControl && lookTarget != null;
            Quaternion desired = Quaternion.identity;
            if (track)
            {
                // Aim origin = eye midpoint (predicted post-correction position), so the gaze FROM THE EYES
                // lands on the target (no pivot parallax). Falls back to the bone pivot when eye bones are
                // missing or aliased to the head.
                Vector3 origin = lookHead.position;
                if (lookLeftEyeBall != null && lookRightEyeBall != null &&
                    lookLeftEyeBall != lookHead && lookRightEyeBall != lookHead)
                {
                    origin = (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f;
                }
                // Predict the post-correction eye position on the animated path. On the recovered path the
                // bone still holds last frame's CORRECTED pose, so the offset already contains the
                // correction and must not be rotated a second time.
                if (!recoveredCleanPose)
                    origin = lookHead.position + _headAimCorrection * (origin - lookHead.position);

                Vector3 dir = lookTarget.position - origin;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    dir.Normalize();
                    Vector3 animatedGaze = animatedRot * _headLocalGaze; // where the face points per animation
                    desired = Quaternion.FromToRotation(animatedGaze, dir);

                    // Clamp to headAngleLimit (same role as the constraint's rotation limits).
                    desired.ToAngleAxis(out float ang, out Vector3 axis);
                    if (ang > 180f) { ang = 360f - ang; axis = -axis; }
                    if (ang > headAngleLimit && !float.IsNaN(axis.x))
                        desired = Quaternion.AngleAxis(headAngleLimit, axis);
                }
                else
                {
                    desired = _headAimCorrection; // target on top of the eyes: hold the current pose
                }
            }

            // Smooth toward the desired correction; identity when not tracking = smooth fade-out.
            float t = Mathf.Clamp01(headSpeed * Time.deltaTime);
            _headAimCorrection = Quaternion.Slerp(_headAimCorrection, desired, t);

            lookHead.rotation = _headAimCorrection * animatedRot;
            _lastWrittenHeadLocalRotation = lookHead.localRotation;
            _hasLastWrittenHeadRotation = true;
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

            // Keep constraint weight and direct-solver activation consistent with the head aim mode
            // (supports switching DirectCalibrated <-> Constraint during play). Runs first so the
            // ownership flags below are correct for this frame.
            SyncHeadAimMode();

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // Sync head/eye MultiAimConstraint angle limits with inspector values (immediate reflect)
            ApplyAngleLimitsToConstraints();
#endif

            // Tell the controller which bones the direct solver owns, so it can skip the dead per-frame
            // virtual-target Lerps (a constraint at weight 0 reads nothing).
            lookTargetController.headDirectDriven = _headAimCalibrated;
            lookTargetController.eyeDirectDriven = EyesDirectDriven;

            // Update virtual targets every frame
            lookTargetController.Update(Time.deltaTime);
        }

        private void LateUpdateLookTarget()
        {
            if (!Application.isPlaying || lookTargetController == null)
                return;

            // Direct head-aim runs first (after animation/rig) so all eye paths below see the corrected
            // head pose. Active only when calibrated (DirectCalibrated mode selected at init); handles its
            // own enable gating and fade internally.
            if (headAimMode == EHeadAimMode.DirectCalibrated)
                DriveCalibratedHeadAim();

            // LateUpdate for BlendShape strategy
            lookTargetController.LateUpdate(Time.deltaTime);

            // Universal direct eye-aim (Transform strategies). Runs after the rig/animation so it overrides
            // both the (disabled) eye constraints and the head rig's effect on its eye children.
            // eyeSpeed == 0 means "no eye tracking": drive nothing and let the animation through, mirroring
            // the head at headSpeed == 0 (whose correction stays identity). Driving with t == 0 would instead
            // freeze the eyes on the direction captured by the first seed and override the animation forever.
            if (EyesDirectDriven && IsEyeControlEffectivelyEnabled && eyeSpeed > 0f)
            {
                DriveUniversalEyeAim();
            }
            else
            {
                // Not driving this frame — the eyes belong to the animation again. Mark it so the next
                // drive re-seeds its smoothed gaze from the animated pose instead of snapping to a stale one.
                _eyeAimDrivingLastFrame = false;
            }
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
            // Delete avatar-specific virtual target group from VirtualTargets container.
            // Strip "(Clone)" to match the name the group was CREATED with
            // (RuntimeFindOrCreateAvatarVirtualTargetGroup / LookTargetController.FindVirtualTargets both
            // strip it); without this, a runtime-Instantiated avatar leaks its group on destroy.
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer != null)
            {
                string avatarGroupName = $"{gameObject.name.Replace("(Clone)", "").Trim()}_VirtualTargets";
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

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
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
                    data.limits = new Vector2(-headAngleLimit, headAngleLimit);
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
                data.limits = new Vector2(-eyeTransformAngleLimit, eyeTransformAngleLimit);
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
#endif

        /// <summary>
        /// Ensure virtual target references exist. Finds existing ones or auto-creates them
        /// for runtime Instantiate scenarios where VirtualTargets don't exist in the scene.
        /// This is the ONLY virtual-target creation path taken by a normal init: the eye virtual
        /// target it produces is what the BlendWeightFluentt strategy aims along, and it carries the
        /// per-frame smoothing and the 0.5m proximity clamp. It must survive the rig removal —
        /// only the constraint/RigBuilder tail below is rigging-specific.
        /// </summary>
        private void EnsureVirtualTargetRefs()
        {
            // Skip if all needed refs are already set
            bool hasHeadRef = headVirtualTargetRef != null;
            bool hasEyeRef = eyeVirtualTargetRef != null;
            bool hasCorrectedRefs = leftEyeVirtualTargetRef != null && rightEyeVirtualTargetRef != null;

            if (hasHeadRef && (eyeControlStrategy == EEyeControlStrategy.TransformCorrected ? hasCorrectedRefs : hasEyeRef))
                return;

            // Find or create VirtualTargets group
            Transform group = RuntimeFindOrCreateAvatarVirtualTargetGroup();
            bool createdNew = false;

            // Head virtual target
            if (headVirtualTargetRef == null && enableHeadControl)
            {
                headVirtualTargetRef = group.Find("HeadVirtualTarget");
                if (headVirtualTargetRef == null && lookHead != null)
                {
                    var go = new GameObject("HeadVirtualTarget");
                    headVirtualTargetRef = go.transform;
                    headVirtualTargetRef.SetParent(group);
                    headVirtualTargetRef.position = lookHead.position + lookHead.forward * 2f;
                    createdNew = true;
                }
            }

            // Eye virtual targets based on strategy
            if (enableEyeControl)
            {
                if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    if (leftEyeVirtualTargetRef == null)
                    {
                        leftEyeVirtualTargetRef = group.Find("LeftEyeVirtualTarget");
                        if (leftEyeVirtualTargetRef == null && lookLeftEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject("LeftEyeVirtualTarget");
                            leftEyeVirtualTargetRef = go.transform;
                            leftEyeVirtualTargetRef.SetParent(group);
                            leftEyeVirtualTargetRef.position = lookLeftEyeBall.position + lookHead.forward * 2f;
                            createdNew = true;
                        }
                    }
                    if (rightEyeVirtualTargetRef == null)
                    {
                        rightEyeVirtualTargetRef = group.Find("RightEyeVirtualTarget");
                        if (rightEyeVirtualTargetRef == null && lookRightEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject("RightEyeVirtualTarget");
                            rightEyeVirtualTargetRef = go.transform;
                            rightEyeVirtualTargetRef.SetParent(group);
                            rightEyeVirtualTargetRef.position = lookRightEyeBall.position + lookHead.forward * 2f;
                            createdNew = true;
                        }
                    }
                }
                else
                {
                    if (eyeVirtualTargetRef == null)
                    {
                        eyeVirtualTargetRef = group.Find("EyeVirtualTarget");
                        if (eyeVirtualTargetRef == null && lookLeftEyeBall != null && lookRightEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject("EyeVirtualTarget");
                            eyeVirtualTargetRef = go.transform;
                            eyeVirtualTargetRef.SetParent(group);
                            Vector3 eyeCenter = (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f;
                            eyeVirtualTargetRef.position = eyeCenter + lookHead.forward * 2f;
                            createdNew = true;
                        }
                    }
                }
            }

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            // Update constraint source objects and rebuild rig if new virtual targets were created
            if (createdNew)
            {
                UpdateConstraintSources();
                var rigBuilder = GetComponent<RigBuilder>();
                if (rigBuilder != null)
                {
                    rigBuilder.Build();
                    if (enableVerboseLogging) Debug.Log("[FluentTAvatarControllerFloatingHead] VirtualTargets auto-created and rig rebuilt for runtime Instantiate");
                }
            }
#else
            _ = createdNew;
#endif
        }

#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
        /// <summary>
        /// Update constraint source objects to point at current virtual target references.
        /// Called after auto-creating VirtualTargets at runtime.
        /// </summary>
        private void UpdateConstraintSources()
        {
            if (headAimConstraint != null && headVirtualTargetRef != null)
            {
                var data = headAimConstraint.data;
                var sources = data.sourceObjects;
                sources.Clear();
                sources.Add(new WeightedTransform(headVirtualTargetRef, 1f));
                data.sourceObjects = sources;
                headAimConstraint.data = data;
            }

            if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
            {
                if (leftEyeAimConstraint != null && leftEyeVirtualTargetRef != null)
                {
                    var data = leftEyeAimConstraint.data;
                    var sources = data.sourceObjects;
                    sources.Clear();
                    sources.Add(new WeightedTransform(leftEyeVirtualTargetRef, 1f));
                    data.sourceObjects = sources;
                    leftEyeAimConstraint.data = data;
                }
                if (rightEyeAimConstraint != null && rightEyeVirtualTargetRef != null)
                {
                    var data = rightEyeAimConstraint.data;
                    var sources = data.sourceObjects;
                    sources.Clear();
                    sources.Add(new WeightedTransform(rightEyeVirtualTargetRef, 1f));
                    data.sourceObjects = sources;
                    rightEyeAimConstraint.data = data;
                }
            }
            else if (eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt)
            {
                Transform eyeVT = eyeVirtualTargetRef;
                if (leftEyeAimConstraint != null && eyeVT != null)
                {
                    var data = leftEyeAimConstraint.data;
                    var sources = data.sourceObjects;
                    sources.Clear();
                    sources.Add(new WeightedTransform(eyeVT, 1f));
                    data.sourceObjects = sources;
                    leftEyeAimConstraint.data = data;
                }
                if (rightEyeAimConstraint != null && eyeVT != null)
                {
                    var data = rightEyeAimConstraint.data;
                    var sources = data.sourceObjects;
                    sources.Clear();
                    sources.Add(new WeightedTransform(eyeVT, 1f));
                    data.sourceObjects = sources;
                    rightEyeAimConstraint.data = data;
                }
            }
        }

        /// <summary>
        /// Sync MultiAimConstraint angle limits with the serialized headAngleLimit/eyeTransformAngleLimit fields.
        /// MultiAimConstraint limits use [SyncSceneToStream], so runtime changes apply on the next frame
        /// without requiring a RigBuilder.Build() rebuild. Eye limits only apply to Transform strategies
        /// (BlendWeightFluentt has no eye MultiAimConstraint).
        /// </summary>
        private void ApplyAngleLimitsToConstraints()
        {
            // Skip bones the direct solver owns: their constraints sit at weight 0, so writing limits
            // into them every frame is dead work. The direct solvers clamp with the same serialized
            // headAngleLimit / eyeTransformAngleLimit values themselves.
            if (headAimConstraint != null && !_headAimCalibrated)
            {
                var headLimits = new Vector2(-headAngleLimit, headAngleLimit);
                var data = headAimConstraint.data;
                if (data.limits != headLimits)
                {
                    data.limits = headLimits;
                    headAimConstraint.data = data;
                }
            }

            if (eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt && !EyesDirectDriven)
            {
                var eyeLimits = new Vector2(-eyeTransformAngleLimit, eyeTransformAngleLimit);
                if (leftEyeAimConstraint != null)
                {
                    var data = leftEyeAimConstraint.data;
                    if (data.limits != eyeLimits)
                    {
                        data.limits = eyeLimits;
                        leftEyeAimConstraint.data = data;
                    }
                }
                if (rightEyeAimConstraint != null)
                {
                    var data = rightEyeAimConstraint.data;
                    if (data.limits != eyeLimits)
                    {
                        data.limits = eyeLimits;
                        rightEyeAimConstraint.data = data;
                    }
                }
            }
        }
#endif

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
    }
}
