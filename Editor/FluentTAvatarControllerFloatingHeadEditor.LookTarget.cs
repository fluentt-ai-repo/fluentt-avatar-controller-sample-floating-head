using FluentT.Animation;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawLookTargetSettings()
        {
            var controller = (FluentTAvatarControllerFloatingHead)target;

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
                var headRenderers = new List<SkinnedMeshRenderer>();

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

                SetFieldValue(controller, "head_skmr", headRenderers);

                EditorUtility.SetDirty(target);
                serializedObject.Update();
                Debug.Log($"{LogPrefix} Found {headRenderers.Count} SkinnedMeshRenderers with blend shapes");
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

            // Track enableHeadControl changes
            var enableHeadControlProp = serializedObject.FindProperty("enableHeadControl");
            bool wasHeadEnabled = enableHeadControlProp.boolValue;
            EditorGUILayout.PropertyField(enableHeadControlProp);

            // Handle Head Control toggle (only in Editor, not Play mode)
            if (!Application.isPlaying && enableHeadControlProp.boolValue != wasHeadEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                if (enableHeadControlProp.boolValue)
                {
                    SetupHeadTrackingOnly(controller);
                }
                else
                {
                    RemoveHeadTrackingOnly(controller);
                }
                serializedObject.Update();
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("headSpeed"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Control Settings", EditorStyles.boldLabel);

            // Track enableEyeControl changes
            var enableEyeControlProp = serializedObject.FindProperty("enableEyeControl");
            bool wasEyeEnabled = enableEyeControlProp.boolValue;
            EditorGUILayout.PropertyField(enableEyeControlProp);

            // Eye control strategy selection
            var eyeControlStrategyProp = serializedObject.FindProperty("eyeControlStrategy");
            EditorGUILayout.PropertyField(eyeControlStrategyProp,
                new GUIContent("Eye Control Strategy", "Choose between Transform (Animation Rigging) or BlendShape control"));

            // Handle Eye Control toggle (only in Editor, not Play mode)
            if (!Application.isPlaying && enableEyeControlProp.boolValue != wasEyeEnabled)
            {
                serializedObject.ApplyModifiedProperties();
                if (enableEyeControlProp.boolValue)
                {
                    SetupEyeTrackingOnly(controller);
                }
                else
                {
                    RemoveEyeTrackingOnly(controller);
                }
                serializedObject.Update();
            }

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

        #region Look Target Rig Auto-Setup

        /// <summary>
        /// Automatically setup Animation Rigging structure for look target tracking
        /// </summary>
        private void SetupLookTargetRig(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            Debug.Log($"{LogPrefix} Setting up look target rig structure...");

            var avatar = controller.gameObject;

            // 1. Ensure RigBuilder exists
            var rigBuilder = avatar.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rigBuilder == null)
            {
                rigBuilder = avatar.AddComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                Debug.Log($"{LogPrefix} Added RigBuilder component");
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
                Debug.Log($"{LogPrefix} Created TargetTracking GameObject");
            }

            // Ensure TargetTracking is active
            if (!targetTracking.gameObject.activeSelf)
            {
                targetTracking.gameObject.SetActive(true);
                Debug.Log($"{LogPrefix} Enabled TargetTracking GameObject");
            }

            // Add Rig component to TargetTracking
            var rig = targetTracking.GetComponent<UnityEngine.Animations.Rigging.Rig>();
            if (rig == null)
            {
                rig = targetTracking.gameObject.AddComponent<UnityEngine.Animations.Rigging.Rig>();
                Debug.Log($"{LogPrefix} Added Rig component to TargetTracking");
            }

            // Ensure rig is enabled with full weight
            rig.weight = 1f;

            // Mark Rig and TargetTracking as dirty
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(targetTracking.gameObject);

            // Ensure rig is in rigBuilder's list
            var rigsList = new List<UnityEngine.Animations.Rigging.Rig>(rigBuilder.layers.Count);
            for (int i = 0; i < rigBuilder.layers.Count; i++)
            {
                rigsList.Add(rigBuilder.layers[i].rig);
            }
            if (!rigsList.Contains(rig))
            {
                rigBuilder.layers.Add(new UnityEngine.Animations.Rigging.RigLayer(rig));
                Debug.Log($"{LogPrefix} Added Rig to RigBuilder layers");
            }

            // Mark RigBuilder as dirty to ensure layers are properly serialized
            EditorUtility.SetDirty(rigBuilder);

            // Get enableHeadControl and enableEyeControl values
            bool enableHeadControl = GetFieldValue<bool>(controller, "enableHeadControl");
            bool enableEyeControl = GetFieldValue<bool>(controller, "enableEyeControl");

            // 3. Setup Head Tracking (only if enabled)
            if (enableHeadControl)
            {
                SetupHeadTracking(controller, targetTracking);
            }

            // 4. Setup Eye Tracking (only if enabled)
            if (enableEyeControl)
            {
                SetupEyeTracking(controller, targetTracking);
            }

            // 5. Set virtual target references directly to avoid GameObject.Find() at runtime
            SetVirtualTargetReferences(controller);

            // Mark dirty for save
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);

            Debug.Log($"{LogPrefix} Look target rig setup complete!");
        }

        /// <summary>
        /// Setup head tracking constraint and virtual target
        /// </summary>
        private void SetupHeadTracking(FluentTAvatarControllerFloatingHead controller, Transform targetTracking)
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
                var headTransform = GetFieldValue<Transform>(controller, "lookHead");

                if (headTransform != null)
                {
                    // Set constrained object
                    var data = headConstraint.data;
                    data.constrainedObject = headTransform;

                    // Set limits for head (-45 to 45 degrees)
                    data.limits = new Vector2(-45f, 45f);

                    headConstraint.data = data;
                }

                Debug.Log($"{LogPrefix} Added Multi-Aim Constraint to HeadTracking");
            }

            // Create or find avatar virtual target group
            Transform avatarVirtualTargetGroup = FindOrCreateAvatarVirtualTargetGroup(controller.gameObject);

            // Create or find head virtual target
            Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
            if (headVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("HeadVirtualTarget");
                headVirtualTarget = virtualTargetGO.transform;
                headVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position in front of head
                var headTransform = GetFieldValue<Transform>(controller, "lookHead");
                if (headTransform != null)
                {
                    headVirtualTarget.position = headTransform.position + headTransform.forward * 2f;
                }
                else
                {
                    headVirtualTarget.position = new Vector3(0, 0, 2);
                }

                Debug.Log($"{LogPrefix} Created HeadVirtualTarget");
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
                Debug.Log($"{LogPrefix} Added HeadVirtualTarget to constraint source objects");
            }

            // Set reference to controller
            SetFieldValue(controller, "headAimConstraint", headConstraint);
        }

        /// <summary>
        /// Setup eye tracking constraints and virtual targets
        /// Creates either single shared target or separate left/right targets based on strategy
        /// </summary>
        private void SetupEyeTracking(FluentTAvatarControllerFloatingHead controller, Transform targetTracking)
        {
            var strategy = GetFieldValue<EEyeControlStrategy>(controller, "eyeControlStrategy");

            // BlendWeightFluentt doesn't need LeftEyeTracking/RightEyeTracking
            if (strategy == EEyeControlStrategy.BlendWeightFluentt)
            {
                // Only create eye virtual target for direction calculation
                CreateEyeVirtualTarget(controller);
                Debug.Log($"{LogPrefix} BlendWeightFluentt mode: Skipping LeftEyeTracking/RightEyeTracking creation");
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
        private Transform CreateEyeVirtualTarget(FluentTAvatarControllerFloatingHead controller)
        {
            Transform avatarVirtualTargetGroup = FindOrCreateAvatarVirtualTargetGroup(controller.gameObject);

            // Create or find eye virtual target
            Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
            if (eyeVirtualTarget == null)
            {
                GameObject virtualTargetGO = new GameObject("EyeVirtualTarget");
                eyeVirtualTarget = virtualTargetGO.transform;
                eyeVirtualTarget.SetParent(avatarVirtualTargetGroup);

                // Position at center between eyes
                var leftEye = GetFieldValue<Transform>(controller, "lookLeftEyeBall");
                var rightEye = GetFieldValue<Transform>(controller, "lookRightEyeBall");
                var headTransform = GetFieldValue<Transform>(controller, "lookHead");

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

                Debug.Log($"{LogPrefix} Created EyeVirtualTarget");
            }

            return eyeVirtualTarget;
        }

        /// <summary>
        /// Create or find separate left/right eye virtual targets (for TransformCorrected mode)
        /// </summary>
        private (Transform leftEye, Transform rightEye) CreateLeftRightEyeVirtualTargets(FluentTAvatarControllerFloatingHead controller)
        {
            Transform avatarVirtualTargetGroup = FindOrCreateAvatarVirtualTargetGroup(controller.gameObject);

            // Get eye transforms
            var leftEye = GetFieldValue<Transform>(controller, "lookLeftEyeBall");
            var rightEye = GetFieldValue<Transform>(controller, "lookRightEyeBall");
            var headTransform = GetFieldValue<Transform>(controller, "lookHead");

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

                Debug.Log($"{LogPrefix} Created LeftEyeVirtualTarget");
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

                Debug.Log($"{LogPrefix} Created RightEyeVirtualTarget");
            }

            return (leftEyeVirtualTarget, rightEyeVirtualTarget);
        }

        /// <summary>
        /// Setup single eye tracking constraint to use shared virtual target
        /// </summary>
        private void SetupSingleEyeTracking(FluentTAvatarControllerFloatingHead controller, Transform targetTracking,
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
                var eyeTransform = GetFieldValue<Transform>(controller, eyeTransformFieldName);

                if (eyeTransform != null)
                {
                    // Set constrained object
                    var data = eyeConstraint.data;
                    data.constrainedObject = eyeTransform;

                    // Set limits for eyes (-20 to 20 degrees)
                    data.limits = new Vector2(-20f, 20f);

                    eyeConstraint.data = data;
                }

                Debug.Log($"{LogPrefix} Added Multi-Aim Constraint to {trackingName}");
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
                Debug.Log($"{LogPrefix} Added EyeVirtualTarget to {trackingName} constraint");
            }

            // Set reference to controller
            SetFieldValue(controller, constraintFieldName, eyeConstraint);
        }

        /// <summary>
        /// Set virtual target references to serialized fields (avoids GameObject.Find at runtime)
        /// </summary>
        private void SetVirtualTargetReferences(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            // Find avatar-specific virtual target group
            Transform avatarVirtualTargetGroup = FindAvatarVirtualTargetGroup(controller.gameObject);
            if (avatarVirtualTargetGroup == null)
            {
                Debug.LogWarning($"{LogPrefix} {controller.gameObject.name}_VirtualTargets not found!");
                return;
            }

            var strategy = GetFieldValue<EEyeControlStrategy>(controller, "eyeControlStrategy");

            // Find and set head virtual target reference
            Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
            SetFieldValue(controller, "headVirtualTargetRef", headVirtualTarget);

            if (strategy == EEyeControlStrategy.TransformCorrected)
            {
                // TransformCorrected mode: set separate left/right eye virtual target references
                Transform leftEyeVirtualTarget = avatarVirtualTargetGroup.Find("LeftEyeVirtualTarget");
                Transform rightEyeVirtualTarget = avatarVirtualTargetGroup.Find("RightEyeVirtualTarget");

                SetFieldValue(controller, "leftEyeVirtualTargetRef", leftEyeVirtualTarget);
                SetFieldValue(controller, "rightEyeVirtualTargetRef", rightEyeVirtualTarget);

                Debug.Log($"{LogPrefix} Set virtual target references for TransformCorrected mode (head + left/right eye)");
            }
            else
            {
                // Transform/BlendShape mode: set single shared eye virtual target reference
                Transform eyeVirtualTarget = avatarVirtualTargetGroup.Find("EyeVirtualTarget");
                SetFieldValue(controller, "eyeVirtualTargetRef", eyeVirtualTarget);

                Debug.Log($"{LogPrefix} Set virtual target references for Transform/BlendShape mode (head + shared eye)");
            }
        }

        /// <summary>
        /// Disable look target rig and destroy all related GameObjects
        /// </summary>
        private void DisableLookTargetRig(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            Debug.Log($"{LogPrefix} Disabling and destroying look target rig...");

            var avatar = controller.gameObject;

            // Remove Head and Eye tracking using dedicated methods
            RemoveHeadTrackingOnly(controller);
            RemoveEyeTrackingOnly(controller);

            // Remove rig from RigBuilder layers and destroy TargetTracking
            Transform targetTracking = avatar.transform.Find("TargetTracking");
            if (targetTracking != null)
            {
                var rigBuilder = avatar.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                if (rigBuilder != null)
                {
                    var rig = targetTracking.GetComponent<UnityEngine.Animations.Rigging.Rig>();
                    if (rig != null)
                    {
                        // Remove rig from rigBuilder's layers
                        for (int i = rigBuilder.layers.Count - 1; i >= 0; i--)
                        {
                            if (rigBuilder.layers[i].rig == rig)
                            {
                                rigBuilder.layers.RemoveAt(i);
                                Debug.Log($"{LogPrefix} Removed Rig from RigBuilder layers");
                                break;
                            }
                        }
                    }
                }

                // Destroy TargetTracking GameObject and all children
                DestroyImmediate(targetTracking.gameObject);
                Debug.Log($"{LogPrefix} Destroyed TargetTracking GameObject");
            }

            // Delete avatar-specific virtual target group
            Transform avatarVirtualTargetGroup = FindAvatarVirtualTargetGroup(avatar);
            if (avatarVirtualTargetGroup != null)
            {
                string groupName = avatarVirtualTargetGroup.name;
                DestroyImmediate(avatarVirtualTargetGroup.gameObject);
                Debug.Log($"{LogPrefix} Deleted {groupName} group");
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);
        }

        /// <summary>
        /// Setup only HeadTracking when enableHeadControl is turned on (while Look Target is already enabled)
        /// </summary>
        private void SetupHeadTrackingOnly(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            var avatar = controller.gameObject;
            Transform targetTracking = avatar.transform.Find("TargetTracking");
            if (targetTracking == null)
            {
                Debug.LogWarning($"{LogPrefix} TargetTracking not found. Please enable Look Target first.");
                return;
            }

            SetupHeadTracking(controller, targetTracking);
            SetVirtualTargetReferences(controller);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);
            Debug.Log($"{LogPrefix} HeadTracking setup complete");
        }

        /// <summary>
        /// Remove only HeadTracking when enableHeadControl is turned off (while Look Target is still enabled)
        /// </summary>
        private void RemoveHeadTrackingOnly(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            var avatar = controller.gameObject;
            Transform targetTracking = avatar.transform.Find("TargetTracking");

            // Clear head constraint and virtual target references
            SetFieldValue(controller, "headAimConstraint", null);
            SetFieldValue(controller, "headVirtualTargetRef", null);

            // Destroy HeadTracking GameObject
            if (targetTracking != null)
            {
                Transform headTracking = targetTracking.Find("HeadTracking");
                if (headTracking != null)
                {
                    DestroyImmediate(headTracking.gameObject);
                    Debug.Log($"{LogPrefix} Destroyed HeadTracking");
                }
            }

            // Destroy HeadVirtualTarget
            Transform avatarVirtualTargetGroup = FindAvatarVirtualTargetGroup(avatar);
            if (avatarVirtualTargetGroup != null)
            {
                Transform headVirtualTarget = avatarVirtualTargetGroup.Find("HeadVirtualTarget");
                if (headVirtualTarget != null)
                {
                    DestroyImmediate(headVirtualTarget.gameObject);
                    Debug.Log($"{LogPrefix} Destroyed HeadVirtualTarget");
                }
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);
        }

        /// <summary>
        /// Setup only EyeTracking when enableEyeControl is turned on (while Look Target is already enabled)
        /// </summary>
        private void SetupEyeTrackingOnly(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            var avatar = controller.gameObject;
            Transform targetTracking = avatar.transform.Find("TargetTracking");
            if (targetTracking == null)
            {
                Debug.LogWarning($"{LogPrefix} TargetTracking not found. Please enable Look Target first.");
                return;
            }

            SetupEyeTracking(controller, targetTracking);
            SetVirtualTargetReferences(controller);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);
            Debug.Log($"{LogPrefix} EyeTracking setup complete");
        }

        /// <summary>
        /// Remove only EyeTracking when enableEyeControl is turned off (while Look Target is still enabled)
        /// </summary>
        private void RemoveEyeTrackingOnly(FluentTAvatarControllerFloatingHead controller)
        {
            if (controller == null)
                return;

            var avatar = controller.gameObject;
            Transform targetTracking = avatar.transform.Find("TargetTracking");

            // Clear eye constraint references
            SetFieldValue(controller, "leftEyeAimConstraint", null);
            SetFieldValue(controller, "rightEyeAimConstraint", null);

            // Clear eye virtual target references
            SetFieldValue(controller, "eyeVirtualTargetRef", null);
            SetFieldValue(controller, "leftEyeVirtualTargetRef", null);
            SetFieldValue(controller, "rightEyeVirtualTargetRef", null);

            // Destroy LeftEyeTracking and RightEyeTracking GameObjects
            if (targetTracking != null)
            {
                Transform leftEyeTracking = targetTracking.Find("LeftEyeTracking");
                Transform rightEyeTracking = targetTracking.Find("RightEyeTracking");
                if (leftEyeTracking != null)
                {
                    DestroyImmediate(leftEyeTracking.gameObject);
                    Debug.Log($"{LogPrefix} Destroyed LeftEyeTracking");
                }
                if (rightEyeTracking != null)
                {
                    DestroyImmediate(rightEyeTracking.gameObject);
                    Debug.Log($"{LogPrefix} Destroyed RightEyeTracking");
                }
            }

            // Destroy EyeVirtualTarget, LeftEyeVirtualTarget, RightEyeVirtualTarget
            Transform avatarVirtualTargetGroup = FindAvatarVirtualTargetGroup(avatar);
            if (avatarVirtualTargetGroup != null)
            {
                string[] eyeTargetNames = { "EyeVirtualTarget", "LeftEyeVirtualTarget", "RightEyeVirtualTarget" };
                foreach (string targetName in eyeTargetNames)
                {
                    Transform eyeTarget = avatarVirtualTargetGroup.Find(targetName);
                    if (eyeTarget != null)
                    {
                        DestroyImmediate(eyeTarget.gameObject);
                        Debug.Log($"{LogPrefix} Destroyed {targetName}");
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(avatar);
        }

        /// <summary>
        /// Auto-find eye look blend shapes from head skinned mesh renderers
        /// </summary>
        private void AutoFindEyeBlendShapes(FluentTAvatarControllerFloatingHead controller)
        {
            var headSkmr = GetFieldValue<List<SkinnedMeshRenderer>>(controller, "head_skmr");

            if (headSkmr == null || headSkmr.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No head skinned mesh renderers found. Please assign head_skmr first.");
                return;
            }

            var eyeBlendShapes = GetFieldValue<EyeBlendShapes>(controller, "eyeBlendShapes");

            if (eyeBlendShapes == null)
            {
                eyeBlendShapes = new EyeBlendShapes();
                SetFieldValue(controller, "eyeBlendShapes", eyeBlendShapes);
            }

            // Initialize and clear all lists
            eyeBlendShapes.eyeLookUpLeftIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookDownLeftIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookInLeftIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookOutLeftIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookUpRightIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookDownRightIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookInRightIdx = new List<EyeBlendShape>();
            eyeBlendShapes.eyeLookOutRightIdx = new List<EyeBlendShape>();

            // Mapping from blend shape name to target list
            var blendShapeMapping = new Dictionary<string, List<EyeBlendShape>>
            {
                { "eyeLookUpLeft", eyeBlendShapes.eyeLookUpLeftIdx },
                { "eyeLookDownLeft", eyeBlendShapes.eyeLookDownLeftIdx },
                { "eyeLookInLeft", eyeBlendShapes.eyeLookInLeftIdx },
                { "eyeLookOutLeft", eyeBlendShapes.eyeLookOutLeftIdx },
                { "eyeLookUpRight", eyeBlendShapes.eyeLookUpRightIdx },
                { "eyeLookDownRight", eyeBlendShapes.eyeLookDownRightIdx },
                { "eyeLookInRight", eyeBlendShapes.eyeLookInRightIdx },
                { "eyeLookOutRight", eyeBlendShapes.eyeLookOutRightIdx }
            };

            int foundCount = 0;

            // Search for blend shapes in all head skinned mesh renderers
            foreach (var skmr in headSkmr)
            {
                if (skmr == null || skmr.sharedMesh == null)
                    continue;

                for (int i = 0; i < skmr.sharedMesh.blendShapeCount; i++)
                {
                    string blendShapeName = skmr.sharedMesh.GetBlendShapeName(i);

                    if (blendShapeMapping.TryGetValue(blendShapeName, out var targetList))
                    {
                        targetList.Add(new EyeBlendShape
                        {
                            skmr = skmr,
                            blendShapeName = blendShapeName,
                            blendShapeIdx = i,
                            scale = 1.0f
                        });
                        foundCount++;
                    }
                }
            }

            // Set default global scale
            eyeBlendShapes.globalScale = 1.0f;

            if (foundCount > 0)
            {
                Debug.Log($"{LogPrefix} Auto-found {foundCount} eye look blend shapes!");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} No eye look blend shapes found. Make sure your avatar has eyeLookUp/Down/In/OutLeft/Right blend shapes.");
            }
        }

        #endregion
    }
}
