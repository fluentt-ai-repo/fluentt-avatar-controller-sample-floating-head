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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("headSkinnedMeshRenderers"));

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
    }
}
#endif
