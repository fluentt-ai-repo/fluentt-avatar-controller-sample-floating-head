using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    [CustomEditor(typeof(FluentTAvatarSampleController))]
    public class FluentTAvatarSampleControllerEditor : UnityEditor.Editor
    {
        private string[] _tabNames = { "Body Animation", "Look Target", "Emotion Tagging", "Server Motion Tagging", "Eye Blink" };

        private string SessionStateKey => $"FluentTAvatarSampleController_SelectedTab_{target.GetInstanceID()}";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Load selected tab from SessionState (persists during editor session only)
            int selectedTab = SessionState.GetInt(SessionStateKey, 0);

            // Draw tab toolbar
            selectedTab = GUILayout.Toolbar(selectedTab, _tabNames);

            // Save selected tab to SessionState
            SessionState.SetInt(SessionStateKey, selectedTab);

            EditorGUILayout.Space();

            // Draw content based on selected tab
            switch (selectedTab)
            {
                case 0: // Body Animation
                    DrawBodyAnimationSettings();
                    break;
                case 1: // Look Target
                    DrawLookTargetSettings();
                    break;
                case 2: // Emotion Tagging
                    DrawEmotionTaggingSettings();
                    break;
                case 3: // Server Motion Tagging
                    DrawServerMotionTaggingSettings();
                    break;
                case 4: // Eye Blink
                    DrawEyeBlinkSettings();
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBodyAnimationSettings()
        {
            EditorGUILayout.LabelField("Body Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatar"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorController"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("idleClips"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("talkingClips"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Body Animation Blend Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("talkingBlendTime"));
        }

        private void DrawLookTargetSettings()
        {
            var controller = (FluentTAvatarSampleController)target;

            EditorGUILayout.LabelField("Look Target Settings", EditorStyles.boldLabel);

            // Track enableLookTarget changes
            var enableLookTargetProp = serializedObject.FindProperty("enableLookTarget");
            bool wasEnabled = enableLookTargetProp.boolValue;

            EditorGUILayout.PropertyField(enableLookTargetProp);

            // Auto-setup rig structure when enableLookTarget is turned on
            if (enableLookTargetProp.boolValue && !wasEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                SetupLookTargetRig(controller);
                serializedObject.Update();
            }
            // Disable rig when turned off
            else if (!enableLookTargetProp.boolValue && wasEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                DisableLookTargetRig(controller);
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookTarget"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cached Head Renderers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("head_skmr"));

            if (GUILayout.Button("Find Head SkinnedMeshRenderers"))
            {
                // Find all SkinnedMeshRenderers (self + children) that have blend shapes
                var headRenderers = new System.Collections.Generic.List<SkinnedMeshRenderer>();

                // Check self first
                if (controller.TryGetComponent<SkinnedMeshRenderer>(out var selfSkmr))
                {
                    if (selfSkmr.sharedMesh != null && selfSkmr.sharedMesh.blendShapeCount > 0)
                    {
                        headRenderers.Add(selfSkmr);
                    }
                }

                // Then check children
                var skinnedMeshRenderers = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skmr in skinnedMeshRenderers)
                {
                    if (skmr.sharedMesh != null && skmr.sharedMesh.blendShapeCount > 0)
                    {
                        headRenderers.Add(skmr);
                    }
                }

                var headSkmrField = typeof(FluentTAvatarSampleController).GetField("head_skmr",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                headSkmrField?.SetValue(controller, headRenderers);

                EditorUtility.SetDirty(target);
                serializedObject.Update();
                Debug.Log($"[FluentTAvatarSampleController] Found {headRenderers.Count} SkinnedMeshRenderers with blend shapes");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookHead"));

            if (GUILayout.Button("Find Look Target Transforms"))
            {
                controller.FindLookTargetTransforms();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Rigging Constraints", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headAimConstraint"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leftEyeAimConstraint"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightEyeAimConstraint"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookLeftEyeBall"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookRightEyeBall"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Strategy Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("idleLookSettings"),
                new GUIContent("Idle Look Settings", "Look target behavior when idle (not talking)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("talkingLookSettings"),
                new GUIContent("Talking Look Settings", "Look target behavior when talking"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Head Control Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableHeadControl"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headSpeed"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Control Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableEyeControl"));

            // Eye control strategy selection
            var eyeControlStrategyProp = serializedObject.FindProperty("eyeControlStrategy");
            EditorGUILayout.PropertyField(eyeControlStrategyProp,
                new GUIContent("Eye Control Strategy", "Choose between Transform (Animation Rigging) or BlendShape control"));

            // Show different settings based on strategy
            var strategy = (EEyeControlStrategy)eyeControlStrategyProp.enumValueIndex;
            if (strategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                EditorGUILayout.HelpBox(
                    "BlendShape Strategy: Controls eye movement using blend shapes (eyeLookUp, eyeLookDown, etc.)\n" +
                    "Make sure your avatar has the required eye blend shapes configured below.",
                    MessageType.Info);

                // Auto Find button
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Auto Find Eye BlendShapes", GUILayout.Width(200), GUILayout.Height(25)))
                {
                    AutoFindEyeBlendShapes(controller);
                    serializedObject.Update();
                    EditorUtility.SetDirty(controller);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeBlendShapes"),
                    new GUIContent("Eye Blend Shapes", "Configure eye look blend shapes"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeAngleLimit"),
                    new GUIContent("Eye Angle Limit", "Maximum angle the eyes can rotate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeAngleLimitThreshold"),
                    new GUIContent("Eye Angle Threshold", "Hysteresis threshold for eye tracking"));
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Transform Strategy: Controls eye movement by rotating eye bone Transforms using Animation Rigging.\n" +
                    "Multi-Aim Constraints above must be configured.",
                    MessageType.Info);
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeSpeed"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gizmo Visualization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("showTargetGizmos"));

            if (serializedObject.FindProperty("showTargetGizmos").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("actualTargetGizmoSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("headVirtualTargetGizmoSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeVirtualTargetGizmoSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("actualTargetColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("headVirtualTargetColor"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("eyeVirtualTargetColor"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawEmotionTaggingSettings()
        {
            EditorGUILayout.LabelField("Client-Side Emotion Tagging Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableClientEmotionTagging"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxEmotionTagsPerSentence"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("wordEmotionMappings"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("emotionMotionMappings"));
        }

        private void DrawServerMotionTaggingSettings()
        {
            EditorGUILayout.LabelField("Server-Side Motion Tagging Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableServerMotionTagging"));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Motion Tag Mappings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("serverMotionTagMappings"));
        }

        private void DrawEyeBlinkSettings()
        {
            EditorGUILayout.LabelField("Eye Blink Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Simple coroutine-based automatic eye blinking.\n\n" +
                "How it works:\n" +
                "1. Automatically finds 'eyeBlinkLeft' and 'eyeBlinkRight' blend shapes in the avatar\n" +
                "2. Controls blend shapes directly using coroutines (no Timeline dependency)\n" +
                "3. Natural timing: close quickly (0.06s) → hold (0.02s) → open slowly (0.10s)\n" +
                "4. Lightweight and efficient implementation\n\n" +
                "Requirements:\n" +
                "• Your avatar must have 'eyeBlinkLeft' and 'eyeBlinkRight' blend shapes",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableEyeBlink"),
                new GUIContent("Enable Eye Blink", "Enable automatic eye blinking"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Timing Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkInterval"),
                new GUIContent("Blink Interval", "Average time between blinks (seconds)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blinkIntervalVariance"),
                new GUIContent("Interval Variance", "Random variance in blink timing (±seconds)"));

            // Show calculated range
            float interval = serializedObject.FindProperty("blinkInterval").floatValue;
            float variance = serializedObject.FindProperty("blinkIntervalVariance").floatValue;
            float minInterval = Mathf.Max(0.1f, interval - variance);
            float maxInterval = interval + variance;
            EditorGUILayout.HelpBox(
                $"Blink will occur every {minInterval:F1}s to {maxInterval:F1}s\n" +
                $"Average: {interval:F1}s",
                MessageType.None);
        }

        #region Look Target Rig Auto-Setup

        /// <summary>
        /// Automatically setup Animation Rigging structure for look target tracking
        /// </summary>
        private void SetupLookTargetRig(FluentTAvatarSampleController controller)
        {
            if (controller == null)
                return;

            Debug.Log("[FluentTAvatarSampleController] Setting up look target rig structure...");

            var avatar = controller.gameObject;

            // 1. Ensure RigBuilder exists
            var rigBuilder = avatar.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = avatar.AddComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                Debug.Log("[FluentTAvatarSampleController] Added RigBuilder component");
            }

            // 2. Find or create TargetTracking
            Transform targetTracking = avatar.transform.Find("TargetTracking");
            if (targetTracking == null)
            {
                GameObject targetTrackingGO = new GameObject("TargetTracking");
                targetTracking = targetTrackingGO.transform;
                targetTracking.SetParent(avatar.transform);
                targetTracking.localPosition = Vector3.zero;
                targetTracking.localRotation = Quaternion.identity;
                targetTracking.localScale = Vector3.one;
                Debug.Log("[FluentTAvatarSampleController] Created TargetTracking GameObject");
            }

            // Ensure TargetTracking is active
            if (!targetTracking.gameObject.activeSelf)
            {
                targetTracking.gameObject.SetActive(true);
                Debug.Log("[FluentTAvatarSampleController] Enabled TargetTracking GameObject");
            }

            // Add Rig component to TargetTracking
            var rig = targetTracking.GetComponent<UnityEngine.Animations.Rigging.Rig>();
            if (rig == null)
            {
                rig = targetTracking.gameObject.AddComponent<UnityEngine.Animations.Rigging.Rig>();
                Debug.Log("[FluentTAvatarSampleController] Added Rig component to TargetTracking");
            }

            // Ensure rig is enabled with full weight
            rig.weight = 1f;

            // Ensure rig is in rigBuilder's list
            var rigsList = new System.Collections.Generic.List<UnityEngine.Animations.Rigging.Rig>(rigBuilder.layers.Count);
            for (int i = 0; i < rigBuilder.layers.Count; i++)
            {
                rigsList.Add(rigBuilder.layers[i].rig);
            }
            if (!rigsList.Contains(rig))
            {
                rigBuilder.layers.Add(new UnityEngine.Animations.Rigging.RigLayer(rig));
                Debug.Log("[FluentTAvatarSampleController] Added Rig to RigBuilder layers");
            }

            // 3. Setup Head Tracking
            SetupHeadTracking(controller, targetTracking);

            // 4. Setup Eye Tracking (if Transform strategy)
            SetupEyeTracking(controller, targetTracking);

            // 5. Set virtual target references directly to avoid GameObject.Find() at runtime
            SetVirtualTargetReferences(controller);

            // Mark dirty for save
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);

            Debug.Log("[FluentTAvatarSampleController] Look target rig setup complete!");
        }

        /// <summary>
        /// Setup head tracking constraint and virtual target
        /// </summary>
        private void SetupHeadTracking(FluentTAvatarSampleController controller, Transform targetTracking)
        {
            // Find or create HeadTracking
            Transform headTracking = targetTracking.Find("HeadTracking");
            if (headTracking == null)
            {
                GameObject headTrackingGO = new GameObject("HeadTracking");
                headTracking = headTrackingGO.transform;
                headTracking.SetParent(targetTracking);
                headTracking.localPosition = Vector3.zero;
                headTracking.localRotation = Quaternion.identity;
                headTracking.localScale = Vector3.one;
            }

            // Add Multi-Aim Constraint
            var headConstraint = headTracking.GetComponent<UnityEngine.Animations.Rigging.MultiAimConstraint>();
            if (headConstraint == null)
            {
                headConstraint = headTracking.gameObject.AddComponent<UnityEngine.Animations.Rigging.MultiAimConstraint>();

                // Find head transform
                controller.FindLookTargetTransforms();
                var headTransformField = typeof(FluentTAvatarSampleController).GetField("lookHead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var headTransform = headTransformField?.GetValue(controller) as Transform;

                if (headTransform != null)
                {
                    // Set constrained object
                    var data = headConstraint.data;
                    data.constrainedObject = headTransform;

                    // Set limits for head (-45 to 45 degrees)
                    data.limits = new Vector2(-45f, 45f);

                    headConstraint.data = data;
                }

                Debug.Log("[FluentTAvatarSampleController] Added Multi-Aim Constraint to HeadTracking");
            }

            // Create or find VirtualTargets container in scene root
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer == null)
            {
                virtualTargetsContainer = new GameObject("VirtualTargets");
                Debug.Log("[FluentTAvatarSampleController] Created VirtualTargets container");
            }

            // Create or find avatar-specific virtual target group
            GameObject avatar = controller.gameObject;
            string avatarGroupName = $"{avatar.name}_VirtualTargets";
            Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
            if (avatarVirtualTargetGroup == null)
            {
                GameObject avatarGroupGO = new GameObject(avatarGroupName);
                avatarVirtualTargetGroup = avatarGroupGO.transform;
                avatarVirtualTargetGroup.SetParent(virtualTargetsContainer.transform);
                Debug.Log($"[FluentTAvatarSampleController] Created {avatarGroupName} group");
            }

            // Create or find head virtual target
            Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
            if (headVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("HeadVirtualTarget");
                headVirtualTarget = virtualTargetGO.transform;
                headVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position in front of head
                var headTransformField = typeof(FluentTAvatarSampleController).GetField("lookHead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var headTransform = headTransformField?.GetValue(controller) as Transform;
                if (headTransform != null)
                {
                    headVirtualTarget.position = headTransform.position + headTransform.forward * 2f;
                }
                else
                {
                    headVirtualTarget.position = new Vector3(0, 0, 2);
                }

                Debug.Log("[FluentTAvatarSampleController] Created HeadVirtualTarget");
            }

            // Add virtual target to constraint source objects
            var constraintData = headConstraint.data;
            var sourceObjects = constraintData.sourceObjects;
            bool hasTarget = false;
            for (int i = 0; i < sourceObjects.Count; i++)
            {
                if (sourceObjects[i].transform == headVirtualTarget)
                {
                    hasTarget = true;
                    break;
                }
            }
            if (!hasTarget)
            {
                sourceObjects.Clear();
                sourceObjects.Add(new UnityEngine.Animations.Rigging.WeightedTransform(headVirtualTarget, 1f));
                constraintData.sourceObjects = sourceObjects;
                headConstraint.data = constraintData;
                Debug.Log("[FluentTAvatarSampleController] Added HeadVirtualTarget to constraint source objects");
            }

            // Set reference to controller
            var headAimConstraintField = typeof(FluentTAvatarSampleController).GetField("headAimConstraint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            headAimConstraintField?.SetValue(controller, headConstraint);
        }

        /// <summary>
        /// Setup eye tracking constraints and virtual targets
        /// Creates either single shared target or separate left/right targets based on strategy
        /// </summary>
        private void SetupEyeTracking(FluentTAvatarSampleController controller, Transform targetTracking)
        {
            // Get eye control strategy using reflection
            var eyeControlStrategyField = typeof(FluentTAvatarSampleController).GetField("eyeControlStrategy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var strategy = (EEyeControlStrategy)eyeControlStrategyField?.GetValue(controller);

            // BlendWeightFluentt doesn't need LeftEyeTracking/RightEyeTracking
            if (strategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                // Only create eye virtual target for direction calculation
                CreateEyeVirtualTarget(controller);
                Debug.Log("[FluentTAvatarSampleController] BlendWeightFluentt mode: Skipping LeftEyeTracking/RightEyeTracking creation");
                return;
            }

            if (strategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: create separate left/right eye virtual targets
                var targets = CreateLeftRightEyeVirtualTargets(controller);

                // Setup left eye to track its own target
                SetupSingleEyeTracking(controller, targetTracking, "LeftEyeTracking",
                    "lookLeftEyeBall", "leftEyeAimConstraint", targets.leftEye);

                // Setup right eye to track its own target
                SetupSingleEyeTracking(controller, targetTracking, "RightEyeTracking",
                    "lookRightEyeBall", "rightEyeAimConstraint", targets.rightEye);
            }
            else if (strategy == EEyeControlStrategy.Transform)
            {
                // Transform mode: create single shared eye virtual target
                Transform eyeVirtualTarget = CreateEyeVirtualTarget(controller);

                // Setup left eye to track the same target
                SetupSingleEyeTracking(controller, targetTracking, "LeftEyeTracking",
                    "lookLeftEyeBall", "leftEyeAimConstraint", eyeVirtualTarget);

                // Setup right eye to track the same target
                SetupSingleEyeTracking(controller, targetTracking, "RightEyeTracking",
                    "lookRightEyeBall", "rightEyeAimConstraint", eyeVirtualTarget);
            }
        }

        /// <summary>
        /// Create or find the single eye virtual target (shared by both eyes)
        /// </summary>
        private Transform CreateEyeVirtualTarget(FluentTAvatarSampleController controller)
        {
            // Find or create VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer == null)
            {
                virtualTargetsContainer = new GameObject("VirtualTargets");
            }

            // Find or create avatar-specific virtual target group
            GameObject avatar = controller.gameObject;
            string avatarGroupName = $"{avatar.name}_VirtualTargets";
            Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
            if (avatarVirtualTargetGroup == null)
            {
                GameObject avatarGroupGO = new GameObject(avatarGroupName);
                avatarVirtualTargetGroup = avatarGroupGO.transform;
                avatarVirtualTargetGroup.SetParent(virtualTargetsContainer.transform);
            }

            // Create or find eye virtual target
            Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
            if (eyeVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("EyeVirtualTarget");
                eyeVirtualTarget = virtualTargetGO.transform;
                eyeVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position at center between eyes
                var leftEyeField = typeof(FluentTAvatarSampleController).GetField("lookLeftEyeBall",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rightEyeField = typeof(FluentTAvatarSampleController).GetField("lookRightEyeBall",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var headField = typeof(FluentTAvatarSampleController).GetField("lookHead",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var leftEye = leftEyeField?.GetValue(controller) as Transform;
                var rightEye = rightEyeField?.GetValue(controller) as Transform;
                var headTransform = headField?.GetValue(controller) as Transform;

                if (leftEye != null && rightEye != null && headTransform != null)
                {
                    // Position at center between eyes, 2m forward
                    Vector3 eyeCenter = (leftEye.position + rightEye.position) * 0.5f;
                    eyeVirtualTarget.position = eyeCenter + headTransform.forward * 2f;
                }
                else
                {
                    eyeVirtualTarget.position = new Vector3(0, 0, 2);
                }

                Debug.Log("[FluentTAvatarSampleController] Created EyeVirtualTarget");
            }

            return eyeVirtualTarget;
        }

        /// <summary>
        /// Create or find separate left/right eye virtual targets (for TransformCorrected mode)
        /// </summary>
        private (Transform leftEye, Transform rightEye) CreateLeftRightEyeVirtualTargets(FluentTAvatarSampleController controller)
        {
            // Find or create VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer == null)
            {
                virtualTargetsContainer = new GameObject("VirtualTargets");
            }

            // Find or create avatar-specific virtual target group
            GameObject avatar = controller.gameObject;
            string avatarGroupName = $"{avatar.name}_VirtualTargets";
            Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
            if (avatarVirtualTargetGroup == null)
            {
                GameObject avatarGroupGO = new GameObject(avatarGroupName);
                avatarVirtualTargetGroup = avatarGroupGO.transform;
                avatarVirtualTargetGroup.SetParent(virtualTargetsContainer.transform);
            }

            // Get eye transforms using reflection
            var leftEyeField = typeof(FluentTAvatarSampleController).GetField("lookLeftEyeBall",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rightEyeField = typeof(FluentTAvatarSampleController).GetField("lookRightEyeBall",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var headField = typeof(FluentTAvatarSampleController).GetField("lookHead",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var leftEye = leftEyeField?.GetValue(controller) as Transform;
            var rightEye = rightEyeField?.GetValue(controller) as Transform;
            var headTransform = headField?.GetValue(controller) as Transform;

            // Create or find left eye virtual target
            Transform leftEyeVirtualTarget = avatarVirtualTargetGroup.Find("LeftEyeVirtualTarget");
            if (leftEyeVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("LeftEyeVirtualTarget");
                leftEyeVirtualTarget = virtualTargetGO.transform;
                leftEyeVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position left eye virtual target
                if (leftEye != null && headTransform != null)
                {
                    leftEyeVirtualTarget.position = leftEye.position + headTransform.forward * 2f;
                }
                else
                {
                    leftEyeVirtualTarget.position = new Vector3(-0.03f, 0, 2);
                }

                Debug.Log("[FluentTAvatarSampleController] Created LeftEyeVirtualTarget");
            }

            // Create or find right eye virtual target
            Transform rightEyeVirtualTarget = avatarVirtualTargetGroup.Find("RightEyeVirtualTarget");
            if (rightEyeVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("RightEyeVirtualTarget");
                rightEyeVirtualTarget = virtualTargetGO.transform;
                rightEyeVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position right eye virtual target
                if (rightEye != null && headTransform != null)
                {
                    rightEyeVirtualTarget.position = rightEye.position + headTransform.forward * 2f;
                }
                else
                {
                    rightEyeVirtualTarget.position = new Vector3(0.03f, 0, 2);
                }

                Debug.Log("[FluentTAvatarSampleController] Created RightEyeVirtualTarget");
            }

            return (leftEyeVirtualTarget, rightEyeVirtualTarget);
        }

        /// <summary>
        /// Setup single eye tracking constraint to use shared virtual target
        /// </summary>
        private void SetupSingleEyeTracking(FluentTAvatarSampleController controller, Transform targetTracking,
            string trackingName, string eyeTransformFieldName, string constraintFieldName, Transform sharedEyeVirtualTarget)
        {
            // Find or create eye tracking
            Transform eyeTracking = targetTracking.Find(trackingName);
            if (eyeTracking == null)
            {
                GameObject eyeTrackingGO = new GameObject(trackingName);
                eyeTracking = eyeTrackingGO.transform;
                eyeTracking.SetParent(targetTracking);
                eyeTracking.localPosition = Vector3.zero;
                eyeTracking.localRotation = Quaternion.identity;
                eyeTracking.localScale = Vector3.one;
            }

            // Add Multi-Aim Constraint
            var eyeConstraint = eyeTracking.GetComponent<UnityEngine.Animations.Rigging.MultiAimConstraint>();
            if (eyeConstraint == null)
            {
                eyeConstraint = eyeTracking.gameObject.AddComponent<UnityEngine.Animations.Rigging.MultiAimConstraint>();

                // Find eye transform
                controller.FindLookTargetTransforms();
                var eyeTransformField = typeof(FluentTAvatarSampleController).GetField(eyeTransformFieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var eyeTransform = eyeTransformField?.GetValue(controller) as Transform;

                if (eyeTransform != null)
                {
                    // Set constrained object
                    var data = eyeConstraint.data;
                    data.constrainedObject = eyeTransform;

                    // Set limits for eyes (-20 to 20 degrees)
                    data.limits = new Vector2(-20f, 20f);

                    eyeConstraint.data = data;
                }

                Debug.Log($"[FluentTAvatarSampleController] Added Multi-Aim Constraint to {trackingName}");
            }

            // Add shared virtual target to constraint source objects
            var constraintData = eyeConstraint.data;
            var sourceObjects = constraintData.sourceObjects;
            bool hasTarget = false;
            for (int i = 0; i < sourceObjects.Count; i++)
            {
                if (sourceObjects[i].transform == sharedEyeVirtualTarget)
                {
                    hasTarget = true;
                    break;
                }
            }
            if (!hasTarget)
            {
                sourceObjects.Clear();
                sourceObjects.Add(new UnityEngine.Animations.Rigging.WeightedTransform(sharedEyeVirtualTarget, 1f));
                constraintData.sourceObjects = sourceObjects;
                eyeConstraint.data = constraintData;
                Debug.Log($"[FluentTAvatarSampleController] Added EyeVirtualTarget to {trackingName} constraint");
            }

            // Set reference to controller
            var constraintField = typeof(FluentTAvatarSampleController).GetField(constraintFieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            constraintField?.SetValue(controller, eyeConstraint);
        }

        /// <summary>
        /// Set virtual target references to serialized fields (avoids GameObject.Find at runtime)
        /// </summary>
        private void SetVirtualTargetReferences(FluentTAvatarSampleController controller)
        {
            if (controller == null)
                return;

            // Find VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer == null)
            {
                Debug.LogWarning("[FluentTAvatarSampleController] VirtualTargets container not found!");
                return;
            }

            // Find avatar-specific virtual target group
            GameObject avatar = controller.gameObject;
            string avatarGroupName = $"{avatar.name}_VirtualTargets";
            Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
            if (avatarVirtualTargetGroup == null)
            {
                Debug.LogWarning($"[FluentTAvatarSampleController] {avatarGroupName} not found!");
                return;
            }

            // Get eye control strategy using reflection
            var eyeControlStrategyField = typeof(FluentTAvatarSampleController).GetField("eyeControlStrategy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var strategy = (EEyeControlStrategy)eyeControlStrategyField?.GetValue(controller);

            // Find virtual targets
            Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");

            // Set serialized field references using reflection
            var headVirtualTargetRefField = typeof(FluentTAvatarSampleController).GetField("headVirtualTargetRef",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            headVirtualTargetRefField?.SetValue(controller, headVirtualTarget);

            if (strategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: set separate left/right eye virtual target references
                Transform leftEyeVirtualTarget = avatarVirtualTargetGroup.Find("LeftEyeVirtualTarget");
                Transform rightEyeVirtualTarget = avatarVirtualTargetGroup.Find("RightEyeVirtualTarget");

                var leftEyeVirtualTargetRefField = typeof(FluentTAvatarSampleController).GetField("leftEyeVirtualTargetRef",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var rightEyeVirtualTargetRefField = typeof(FluentTAvatarSampleController).GetField("rightEyeVirtualTargetRef",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                leftEyeVirtualTargetRefField?.SetValue(controller, leftEyeVirtualTarget);
                rightEyeVirtualTargetRefField?.SetValue(controller, rightEyeVirtualTarget);

                Debug.Log("[FluentTAvatarSampleController] Set virtual target references for TransformCorrected mode (head + left/right eye)");
            }
            else
            {
                // Transform/BlendShape mode: set single shared eye virtual target reference
                Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");

                var eyeVirtualTargetRefField = typeof(FluentTAvatarSampleController).GetField("eyeVirtualTargetRef",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                eyeVirtualTargetRefField?.SetValue(controller, eyeVirtualTarget);

                Debug.Log("[FluentTAvatarSampleController] Set virtual target references for Transform/BlendShape mode (head + shared eye)");
            }
        }

        /// <summary>
        /// Disable look target rig (without destroying it)
        /// </summary>
        private void DisableLookTargetRig(FluentTAvatarSampleController controller)
        {
            if (controller == null)
                return;

            Debug.Log("[FluentTAvatarSampleController] Disabling look target rig...");

            var avatar = controller.gameObject;
            Transform targetTracking = avatar.transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                var rig = targetTracking.GetComponent<UnityEngine.Animations.Rigging.Rig>();
                if (rig != null)
                {
                    rig.weight = 0f;
                }
                targetTracking.gameObject.SetActive(false);
                Debug.Log("[FluentTAvatarSampleController] Disabled TargetTracking");
            }

            // Delete avatar-specific virtual target group from VirtualTargets container
            GameObject virtualTargetsContainer = GameObject.Find("VirtualTargets");
            if (virtualTargetsContainer != null)
            {
                string avatarGroupName = $"{avatar.name}_VirtualTargets";
                Transform avatarVirtualTargetGroup = virtualTargetsContainer.transform.Find(avatarGroupName);
                if (avatarVirtualTargetGroup != null)
                {
                    DestroyImmediate(avatarVirtualTargetGroup.gameObject);
                    Debug.Log($"[FluentTAvatarSampleController] Deleted {avatarGroupName} group");
                }
            }

            EditorUtility.SetDirty(controller);
        }

        /// <summary>
        /// Auto-find eye look blend shapes from head skinned mesh renderers
        /// </summary>
        private void AutoFindEyeBlendShapes(FluentTAvatarSampleController controller)
        {
            // Get head skinned mesh renderers using reflection
            var headSkmrField = typeof(FluentTAvatarSampleController).GetField("head_skmr",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var headSkmr = headSkmrField?.GetValue(controller) as System.Collections.Generic.List<SkinnedMeshRenderer>;

            if (headSkmr == null || headSkmr.Count == 0)
            {
                Debug.LogWarning("[FluentTAvatarSampleController] No head skinned mesh renderers found. Please assign head_skmr first.");
                return;
            }

            // Get eyeBlendShapes field
            var eyeBlendShapesField = typeof(FluentTAvatarSampleController).GetField("eyeBlendShapes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var eyeBlendShapes = eyeBlendShapesField?.GetValue(controller) as EyeBlendShapes;

            if (eyeBlendShapes == null)
            {
                eyeBlendShapes = new EyeBlendShapes();
                eyeBlendShapesField?.SetValue(controller, eyeBlendShapes);
            }

            // Initialize lists if null
            eyeBlendShapes.eyeLookUpLeftIdx = eyeBlendShapes.eyeLookUpLeftIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookDownLeftIdx = eyeBlendShapes.eyeLookDownLeftIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookInLeftIdx = eyeBlendShapes.eyeLookInLeftIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookOutLeftIdx = eyeBlendShapes.eyeLookOutLeftIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookUpRightIdx = eyeBlendShapes.eyeLookUpRightIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookDownRightIdx = eyeBlendShapes.eyeLookDownRightIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookInRightIdx = eyeBlendShapes.eyeLookInRightIdx ?? new System.Collections.Generic.List<EyeBlendShape>();
            eyeBlendShapes.eyeLookOutRightIdx = eyeBlendShapes.eyeLookOutRightIdx ?? new System.Collections.Generic.List<EyeBlendShape>();

            // Clear existing
            eyeBlendShapes.eyeLookUpLeftIdx.Clear();
            eyeBlendShapes.eyeLookDownLeftIdx.Clear();
            eyeBlendShapes.eyeLookInLeftIdx.Clear();
            eyeBlendShapes.eyeLookOutLeftIdx.Clear();
            eyeBlendShapes.eyeLookUpRightIdx.Clear();
            eyeBlendShapes.eyeLookDownRightIdx.Clear();
            eyeBlendShapes.eyeLookInRightIdx.Clear();
            eyeBlendShapes.eyeLookOutRightIdx.Clear();

            int foundCount = 0;

            // Search for blend shapes in all head skinned mesh renderers
            foreach (var skmr in headSkmr)
            {
                if (skmr == null || skmr.sharedMesh == null)
                    continue;

                for (int i = 0; i < skmr.sharedMesh.blendShapeCount; i++)
                {
                    string blendShapeName = skmr.sharedMesh.GetBlendShapeName(i);

                    // Match blend shape names
                    if (blendShapeName == "eyeLookUpLeft")
                    {
                        eyeBlendShapes.eyeLookUpLeftIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookDownLeft")
                    {
                        eyeBlendShapes.eyeLookDownLeftIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookInLeft")
                    {
                        eyeBlendShapes.eyeLookInLeftIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookOutLeft")
                    {
                        eyeBlendShapes.eyeLookOutLeftIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookUpRight")
                    {
                        eyeBlendShapes.eyeLookUpRightIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookDownRight")
                    {
                        eyeBlendShapes.eyeLookDownRightIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookInRight")
                    {
                        eyeBlendShapes.eyeLookInRightIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                    else if (blendShapeName == "eyeLookOutRight")
                    {
                        eyeBlendShapes.eyeLookOutRightIdx.Add(new EyeBlendShape { skmr = skmr, blendShapeName = blendShapeName, blendShapeIdx = i, scale = 1.0f });
                        foundCount++;
                    }
                }
            }

            // Set default global scale
            eyeBlendShapes.globalScale = 1.0f;

            if (foundCount > 0)
            {
                Debug.Log($"[FluentTAvatarSampleController] Auto-found {foundCount} eye look blend shapes!");
            }
            else
            {
                Debug.LogWarning("[FluentTAvatarSampleController] No eye look blend shapes found. Make sure your avatar has eyeLookUp/Down/In/OutLeft/Right blend shapes.");
            }
        }

        #endregion
    }
}

