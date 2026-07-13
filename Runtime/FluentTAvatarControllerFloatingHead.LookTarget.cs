using UnityEngine;

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
        // Max angle the head can rotate toward the target.
        [SerializeField] [Range(0f, 90f)] private float headAngleLimit = 45f;

        [SerializeField] private bool enableEyeControl = true;
        [SerializeField] [Range(0f, 20f)] private float eyeSpeed = 10f;

        // Eye control strategy
        [SerializeField] private EEyeControlStrategy eyeControlStrategy = EEyeControlStrategy.Transform;
        [SerializeField] private EyeBlendShapes eyeBlendShapes = new();
        // BlendShape strategy: angle cutoff (stop eye tracking beyond this angle) + hysteresis threshold
        [SerializeField] [Range(0f, 45f)] private float eyeAngleLimit = 10f;
        [SerializeField] [Range(0f, 15f)] private float eyeAngleLimitThreshold = 5f;
        // Transform/TransformCorrected strategy: max angle the eyes can rotate toward the target.
        [SerializeField] [Range(0f, 90f)] private float eyeTransformAngleLimit = 20f;

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


            // Initialize LookTargetController
            lookTargetController = new LookTargetController();

            // Configure LookTargetController
            lookTargetController.SetAvatarRoot(transform);
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


            // Enable
            lookTargetController.Enable();

            // Universal eye-aim: calibrate while the eye bones are still in bind pose (Start runs before the
            // first animation evaluation). Engages for every Transform/TransformCorrected eye strategy.
            if (WillUseUniversalEyeAim())
                CalibrateUniversalEyeAim();

            // Head direct-aim: calibrate the head bone's true facial forward while still in bind pose.
            // Per-frame gating on enableHeadControl happens in the drive itself.
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
        /// Whether the rest-calibrated direct-drive eye solver owns the eyes. It is the only eye path for
        /// the Transform/TransformCorrected strategies — exact for ANY bone axis orientation (incl.
        /// non-cardinal gaze axes) and any scale incl. mirrored bones. BlendWeightFluentt drives the eyes
        /// via blend shapes and never uses it.
        /// </summary>
        private bool WillUseUniversalEyeAim()
        {
            return enableEyeControl && eyeControlStrategy != EEyeControlStrategy.BlendWeightFluentt;
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
        /// Ensure the direct head-aim solver is calibrated (idempotent; reuses the bind-pose capture, so
        /// it is safe to call every frame and mid-play). The head is always direct-driven now that the
        /// legacy MultiAimConstraint path is gone.
        /// </summary>
        private void SyncHeadAimMode()
        {
            if (!_headAimCalibrated && lookHead != null)
                CalibrateHeadAim();
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

            // Direct head-aim runs first (after animation) so all eye paths below see the corrected head
            // pose. Handles its own enable gating and fade internally.
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

        // CleanupVirtualTargets() was removed together with the scene-root container: the virtual targets
        // now live under this avatar and Unity destroys them along with it. Manual cleanup would also
        // throw ("Destroy may not be called from edit mode") if the avatar is deleted outside play mode.

        #endregion

        #region Runtime Rig Setup/Destroy


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
            // Skip if every ref the enabled features actually need is already set. A disabled feature
            // needs no ref: gating on hasHeadRef unconditionally used to make this check unsatisfiable
            // for avatars with enableHeadControl off (every FloatingHead prefab ships that way, so they
            // never get a head ref), and the container was rebuilt on every init even when the eye refs
            // were already wired up.
            bool hasHeadRef = headVirtualTargetRef != null;
            bool hasEyeRef = eyeVirtualTargetRef != null;
            bool hasCorrectedRefs = leftEyeVirtualTargetRef != null && rightEyeVirtualTargetRef != null;

            bool headSatisfied = !enableHeadControl || hasHeadRef;
            bool eyeSatisfied = !enableEyeControl ||
                (eyeControlStrategy == EEyeControlStrategy.TransformCorrected ? hasCorrectedRefs : hasEyeRef);

            if (headSatisfied && eyeSatisfied)
                return;

            // Find or create the virtual target container under this avatar
            Transform group = RuntimeFindOrCreateAvatarVirtualTargetGroup();

            // Head virtual target
            if (headVirtualTargetRef == null && enableHeadControl)
            {
                headVirtualTargetRef = group.Find(LookTargetController.HeadAnchorName);
                if (headVirtualTargetRef == null && lookHead != null)
                {
                    var go = new GameObject(LookTargetController.HeadAnchorName);
                    headVirtualTargetRef = go.transform;
                    headVirtualTargetRef.SetParent(group);
                    headVirtualTargetRef.position = lookHead.position + lookHead.forward * 2f;
                }
            }

            // Eye virtual targets based on strategy
            if (enableEyeControl)
            {
                if (eyeControlStrategy == EEyeControlStrategy.TransformCorrected)
                {
                    if (leftEyeVirtualTargetRef == null)
                    {
                        leftEyeVirtualTargetRef = group.Find(LookTargetController.LeftEyeAnchorName);
                        if (leftEyeVirtualTargetRef == null && lookLeftEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject(LookTargetController.LeftEyeAnchorName);
                            leftEyeVirtualTargetRef = go.transform;
                            leftEyeVirtualTargetRef.SetParent(group);
                            leftEyeVirtualTargetRef.position = lookLeftEyeBall.position + lookHead.forward * 2f;
                        }
                    }
                    if (rightEyeVirtualTargetRef == null)
                    {
                        rightEyeVirtualTargetRef = group.Find(LookTargetController.RightEyeAnchorName);
                        if (rightEyeVirtualTargetRef == null && lookRightEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject(LookTargetController.RightEyeAnchorName);
                            rightEyeVirtualTargetRef = go.transform;
                            rightEyeVirtualTargetRef.SetParent(group);
                            rightEyeVirtualTargetRef.position = lookRightEyeBall.position + lookHead.forward * 2f;
                        }
                    }
                }
                else
                {
                    if (eyeVirtualTargetRef == null)
                    {
                        eyeVirtualTargetRef = group.Find(LookTargetController.EyeAnchorName);
                        if (eyeVirtualTargetRef == null && lookLeftEyeBall != null && lookRightEyeBall != null && lookHead != null)
                        {
                            var go = new GameObject(LookTargetController.EyeAnchorName);
                            eyeVirtualTargetRef = go.transform;
                            eyeVirtualTargetRef.SetParent(group);
                            Vector3 eyeCenter = (lookLeftEyeBall.position + lookRightEyeBall.position) * 0.5f;
                            eyeVirtualTargetRef.position = eyeCenter + lookHead.forward * 2f;
                        }
                    }
                }
            }

        }


        /// <summary>
        /// Helper: Find or create this avatar's virtual target container at runtime.
        /// </summary>
        /// <remarks>
        /// The container is a DIRECT CHILD of the avatar root, never of a bone. Two rules make this safe,
        /// and breaking either one is what corrupts the rig:
        ///   - Leaf only, no components. Adding a leaf leaves SkinnedMeshRenderer.bones (a Transform
        ///     reference array) and every authored clip's binding path untouched. Re-parenting an existing
        ///     bone or mesh node under it would not.
        ///   - Never under an animated bone. The virtual target is the smoothing proxy the head/eye aim
        ///     converges through, so parenting it to the head would let the head drag its own aim target.
        /// It used to be created at the scene root because the old MultiAimConstraint required its source
        /// objects to sit outside the rig. That rigging was removed in v0.5.0, and owning the container
        /// removes the scene-wide GameObject.Find + "(Clone)" name matching that leaked and cross-wired
        /// groups between avatars.
        /// </remarks>
        private Transform RuntimeFindOrCreateAvatarVirtualTargetGroup()
        {
            Transform group = transform.Find(LookTargetController.VirtualTargetsContainerName);
            if (group != null)
                return group;

            var groupGO = new GameObject(LookTargetController.VirtualTargetsContainerName);
            group = groupGO.transform;

            // Identity local TRS. The anchors below are positioned in WORLD space right after SetParent,
            // so any invertible parent matrix is fine — including the non-unit root scales and the 180deg
            // yaw some avatars ship with.
            group.SetParent(transform, worldPositionStays: false);
            group.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.localScale = Vector3.one;

            if (enableVerboseLogging)
                Debug.Log($"[FluentTAvatarControllerFloatingHead] Created {LookTargetController.VirtualTargetsContainerName} under {gameObject.name}");

            return group;
        }

        #endregion
    }
}
