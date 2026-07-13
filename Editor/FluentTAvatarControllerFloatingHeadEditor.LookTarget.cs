using FluentT.Animation;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    /// <summary>
    /// Look Target Inspector UI drawing
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_idleLook = new("Idle Look Settings", "Look target behavior when idle (not talking)");
        private static readonly GUIContent gc_talkingLook = new("Talking Look Settings", "Look target behavior when talking");
        private static readonly GUIContent gc_eyeStrategy = new("Eye Control Strategy", "Choose between Transform (direct bone drive) or BlendShape eye control");
        private static readonly GUIContent gc_eyeBlendShapes = new("Eye Blend Shapes", "Configure eye look blend shapes");
        private static readonly GUIContent gc_eyeAngleLimit = new("Eye Angle Limit", "Maximum angle the eyes can rotate");
        private static readonly GUIContent gc_eyeAngleThreshold = new("Eye Angle Threshold", "Hysteresis threshold for eye tracking");
        private static readonly GUIContent gc_headAngleLimit = new("Head Angle Limit", "Maximum angle the head can rotate toward the target");
        private static readonly GUIContent gc_eyeAngleLimitTransform = new("Eye Angle Limit", "Maximum angle the eyes can rotate toward the target");
        private static readonly GUIContent[] gc_eyeStrategyOptions = { new("Blend Weight (Fluentt)"), new("Transform") };

        private void DrawLookTargetSettings()
        {
            var controller = (FluentTAvatarControllerFloatingHead)target;

            EditorGUILayout.LabelField("Look Target Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableLookTargetProp);
            EditorGUILayout.PropertyField(lookTargetProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cached Head Renderers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(headSkinnedMeshRenderersProp);

            if (GUILayout.Button("Find Head SkinnedMeshRenderers"))
            {
                // Find all SkinnedMeshRenderers (self + children) that have blend shapes
                var headRenderers = new List<SkinnedMeshRenderer>();

                if (controller.TryGetComponent<SkinnedMeshRenderer>(out var selfSkmr))
                {
                    if (selfSkmr.sharedMesh != null && selfSkmr.sharedMesh.blendShapeCount > 0)
                        headRenderers.Add(selfSkmr);
                }

                var skinnedMeshRenderers = controller.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    if (skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                        headRenderers.Add(skinnedMeshRenderer);
                }

                SetFieldValue(controller, "headSkinnedMeshRenderers", headRenderers);

                EditorUtility.SetDirty(target);
                serializedObject.Update();
                Debug.Log($"{LogPrefix} Found {headRenderers.Count} SkinnedMeshRenderers with blend shapes");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(lookHeadProp);

            if (GUILayout.Button("Find Look Target Transforms"))
            {
                controller.FindLookTargetTransforms();
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Transforms", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(lookLeftEyeBallProp);
            EditorGUILayout.PropertyField(lookRightEyeBallProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look Target Strategy Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(idleLookSettingsProp, gc_idleLook);
            EditorGUILayout.PropertyField(talkingLookSettingsProp, gc_talkingLook);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Head Control Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableHeadControlProp);
            EditorGUILayout.HelpBox(
                "The head bone's true facial forward is measured against the avatar root at init and the bone " +
                "is driven directly each LateUpdate (aiming from the eye midpoint), so it aims exactly even on " +
                "rigs whose head-bone local +Z is not the gaze direction.",
                MessageType.None);
            EditorGUILayout.PropertyField(headSpeedProp);
            EditorGUILayout.PropertyField(headAngleLimitProp, gc_headAngleLimit);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Control Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableEyeControlProp);

            // Eye control strategy selection (2-item dropdown).
            // TransformCorrected is kept in the enum for backward-compat but hidden behind "Transform";
            // existing TransformCorrected/Transform values both display as "Transform" and are only
            // rewritten when the user actually changes the dropdown (so saved values are preserved).
            EEyeControlStrategy curStrategy = (EEyeControlStrategy)eyeControlStrategyProp.enumValueIndex;
            int displayIndex = curStrategy == EEyeControlStrategy.BlendWeightFluentt ? 0 : 1;
            int newIndex = EditorGUILayout.Popup(gc_eyeStrategy, displayIndex, gc_eyeStrategyOptions);
            if (newIndex != displayIndex)
                eyeControlStrategyProp.enumValueIndex = newIndex == 0
                    ? (int)EEyeControlStrategy.BlendWeightFluentt
                    : (int)EEyeControlStrategy.TransformCorrected;

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

                EditorGUILayout.PropertyField(eyeBlendShapesProp, gc_eyeBlendShapes);
                EditorGUILayout.PropertyField(eyeAngleLimitProp, gc_eyeAngleLimit);
                EditorGUILayout.PropertyField(eyeAngleLimitThresholdProp, gc_eyeAngleThreshold);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Transform Strategy: rotates the eye bones directly with a rest-calibrated solver — robust " +
                    "to any bone axis orientation and any scale, including mirrored (negative-scale) eye bones.",
                    MessageType.Info);

                EditorGUILayout.PropertyField(eyeTransformAngleLimitProp, gc_eyeAngleLimitTransform);
            }

            EditorGUILayout.PropertyField(eyeSpeedProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Gizmo Visualization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(showTargetGizmosProp);

            if (showTargetGizmosProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(actualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(headVirtualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(eyeVirtualTargetGizmoSizeProp);
                EditorGUILayout.PropertyField(actualTargetColorProp);
                EditorGUILayout.PropertyField(headVirtualTargetColorProp);
                EditorGUILayout.PropertyField(eyeVirtualTargetColorProp);
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Auto-find eye look blend shapes from head skinned mesh renderers
        /// </summary>
        private void AutoFindEyeBlendShapes(FluentTAvatarControllerFloatingHead controller)
        {
            var headSkmr = GetFieldValue<List<SkinnedMeshRenderer>>(controller, "headSkinnedMeshRenderers");

            if (headSkmr == null || headSkmr.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} No head skinned mesh renderers found. Please assign headSkinnedMeshRenderers first.");
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
            foreach (var skinnedMeshRenderer in headSkmr)
            {
                if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
                    continue;

                for (int i = 0; i < skinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    string blendShapeName = skinnedMeshRenderer.sharedMesh.GetBlendShapeName(i);

                    if (blendShapeMapping.TryGetValue(blendShapeName, out var targetList))
                    {
                        targetList.Add(new EyeBlendShape
                        {
                            skinnedMeshRenderer = skinnedMeshRenderer,
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
    }
}
