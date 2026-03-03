#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    /// <summary>
    /// Head/Eye tracking enable/disable toggle and BlendShape auto-search
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
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
#endif
