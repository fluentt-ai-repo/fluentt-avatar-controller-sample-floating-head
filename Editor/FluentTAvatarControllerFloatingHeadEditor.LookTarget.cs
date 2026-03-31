#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
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
        private static readonly GUIContent gc_eyeStrategy = new("Eye Control Strategy", "Choose between Transform (Animation Rigging) or BlendShape control");
        private static readonly GUIContent gc_eyeBlendShapes = new("Eye Blend Shapes", "Configure eye look blend shapes");
        private static readonly GUIContent gc_eyeAngleLimit = new("Eye Angle Limit", "Maximum angle the eyes can rotate");
        private static readonly GUIContent gc_eyeAngleThreshold = new("Eye Angle Threshold", "Hysteresis threshold for eye tracking");

        private void DrawLookTargetSettings()
        {
            var controller = (FluentTAvatarControllerFloatingHead)target;

            EditorGUILayout.LabelField("Look Target Settings", EditorStyles.boldLabel);

            // Track enableLookTarget changes
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

            EditorGUILayout.PropertyField(lookTargetProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cached Head Renderers", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(headSkinnedMeshRenderersProp);

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
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    if (skinnedMeshRenderer.sharedMesh != null && skinnedMeshRenderer.sharedMesh.blendShapeCount > 0)
                    {
                        headRenderers.Add(skinnedMeshRenderer);
                    }
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
            EditorGUILayout.LabelField("Animation Rigging Constraints", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(headAimConstraintProp);
            EditorGUILayout.PropertyField(leftEyeAimConstraintProp);
            EditorGUILayout.PropertyField(rightEyeAimConstraintProp);

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

            // Track enableHeadControl changes
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

            EditorGUILayout.PropertyField(headSpeedProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Eye Control Settings", EditorStyles.boldLabel);

            // Track enableEyeControl changes
            bool wasEyeEnabled = enableEyeControlProp.boolValue;
            EditorGUILayout.PropertyField(enableEyeControlProp);

            // Eye control strategy selection
            EditorGUILayout.PropertyField(eyeControlStrategyProp, gc_eyeStrategy);

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

                EditorGUILayout.PropertyField(eyeBlendShapesProp, gc_eyeBlendShapes);
                EditorGUILayout.PropertyField(eyeAngleLimitProp, gc_eyeAngleLimit);
                EditorGUILayout.PropertyField(eyeAngleLimitThresholdProp, gc_eyeAngleThreshold);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Transform Strategy: Controls eye movement by rotating eye bone Transforms using Animation Rigging.\n" +
                    "Multi-Aim Constraints above must be configured.",
                    MessageType.Info);
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

            // Runtime Rig Control (Play mode only)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Runtime Rig Control", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Setup Rig"))
                    controller.SetupLookTargetRigAtRuntime();
                if (GUILayout.Button("Destroy Rig"))
                    controller.DestroyLookTargetRigAtRuntime();
                if (GUILayout.Button("Rebuild"))
                    controller.RebuildRig();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
