using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private static readonly GUIContent gc_oneShotMotions = new("One-Shot Motions", "List of one-shot motions that can be triggered by motionId");
        private static readonly GUIContent gc_oneShotGroups = new("One-Shot Motion Groups", "Groups of clips that loop with weighted random selection");
        private static readonly GUIContent gc_onMotionStarted = new("On Motion Started", "Fired when a one-shot motion starts playing. Parameter: motionId");
        private static readonly GUIContent gc_onMotionEnded = new("On Motion Ended", "Fired when a one-shot motion finishes and returns to idle. Parameter: motionId");

        private int debugSelectedMotionIndex;
        private int debugSelectedGroupIndex;

        // Reusable collections to avoid per-frame allocation
        private readonly Dictionary<string, int> validationSeenIds = new();
        private readonly List<string> tempIdList = new();

        // Track clip references to detect changes for auto override detection
        private readonly Dictionary<string, int> previousClipInstanceIds = new();

        // Eye BlendShape curve prefixes used for auto-detection
        private static readonly string[] EYE_BLEND_SHAPE_PREFIXES = new[]
        {
            "blendShape.eyeLookUp",
            "blendShape.eyeLookDown",
            "blendShape.eyeLookIn",
            "blendShape.eyeLookOut"
        };

        // Eye Blink curve prefixes used for auto-detection
        private static readonly string[] EYE_BLINK_PREFIXES = new[]
        {
            "blendShape.eyeBlinkLeft",
            "blendShape.eyeBlinkRight"
        };

        private void DrawOneShotMotionSettings()
        {
            EditorGUILayout.LabelField("One-Shot Motion", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Register animation clips that can be played once on demand via PlayOneShotMotion(motionId).\n\n" +
                "Each motion is identified by a unique motionId string, which external systems (e.g. Flutter bridge) " +
                "use to trigger playback. The motion plays once and automatically returns to idle.\n\n" +
                "Uses the same Animator layer as Gesture Animation (cannot play simultaneously).",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Registered Motions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(oneShotMotionsProp, gc_oneShotMotions);
            DrawOneShotMotionDropArea(oneShotMotionsProp);
            AutoDetectOverridesForArray(oneShotMotionsProp);

            // Validation: check for duplicate motionIds and empty entries
            ValidateOneShotMotions(oneShotMotionsProp);

            // === Motion Groups ===
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Motion Groups", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Groups play random clips in a loop (weighted) until stopped or TalkMotion starts.\n" +
                "Use PlayOneShotMotionGroup(groupId) to trigger.",
                MessageType.Info);
            EditorGUILayout.PropertyField(oneShotMotionGroupsProp, gc_oneShotGroups);
            AutoDetectOverridesForGroupArray(oneShotMotionGroupsProp);
            ValidateOneShotMotionGroups(oneShotMotionGroupsProp);

            // === Debug ===
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play mode only: debug buttons will appear here at runtime.", MessageType.None);
            }
            else
            {
                var controller = (FluentTAvatarControllerFloatingHead)target;

                // Stop button
                using (new EditorGUI.DisabledGroupScope(!controller.IsOneShotMotionPlaying() && !controller.IsOneShotGroupLooping()))
                {
                    if (GUILayout.Button("Stop"))
                    {
                        controller.StopOneShotMotion();
                    }
                }

                EditorGUILayout.Space(2);

                // Individual motion: dropdown + Play button
                var motionIds = BuildMotionIdList(oneShotMotionsProp);
                if (motionIds.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    debugSelectedMotionIndex = Mathf.Clamp(debugSelectedMotionIndex, 0, motionIds.Length - 1);
                    debugSelectedMotionIndex = EditorGUILayout.Popup(debugSelectedMotionIndex, motionIds);
                    if (GUILayout.Button("Play", GUILayout.Width(50)))
                    {
                        controller.PlayOneShotMotion(motionIds[debugSelectedMotionIndex]);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // Group: dropdown + Loop button
                var groupIds = BuildGroupIdList(oneShotMotionGroupsProp);
                if (groupIds.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    debugSelectedGroupIndex = Mathf.Clamp(debugSelectedGroupIndex, 0, groupIds.Length - 1);
                    debugSelectedGroupIndex = EditorGUILayout.Popup(debugSelectedGroupIndex, groupIds);
                    if (GUILayout.Button("Loop", GUILayout.Width(50)))
                    {
                        controller.PlayOneShotMotionGroup(groupIds[debugSelectedGroupIndex]);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Callbacks", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(onOneShotMotionStartedProp, gc_onMotionStarted);
            EditorGUILayout.PropertyField(onOneShotMotionEndedProp, gc_onMotionEnded);
        }

        /// <summary>
        /// Drag-and-drop area for OneShotMotionEntry. Sets motionId from clip name.
        /// </summary>
        private void DrawOneShotMotionDropArea(SerializedProperty arrayProp)
        {
            var dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drop AnimationClips here (motionId = clip name)", EditorStyles.helpBox);

            var evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition)) return;

            if (evt.type == EventType.DragUpdated)
            {
                bool hasClip = false;
                foreach (var obj in DragAndDrop.objectReferences)
                    if (obj is AnimationClip) { hasClip = true; break; }
                DragAndDrop.visualMode = hasClip ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is AnimationClip clip)
                    {
                        int idx = arrayProp.arraySize;
                        arrayProp.InsertArrayElementAtIndex(idx);
                        var newEntry = arrayProp.GetArrayElementAtIndex(idx);
                        newEntry.FindPropertyRelative("clip").objectReferenceValue = clip;
                        newEntry.FindPropertyRelative("weight").floatValue = 1f;
                        newEntry.FindPropertyRelative("motionId").stringValue = clip.name;
                        newEntry.FindPropertyRelative("overrideEyeControl").boolValue = false;
                        newEntry.FindPropertyRelative("overrideEyeBlink").boolValue = false;
                        AutoDetectOverridesForEntry(newEntry);
                    }
                }
                serializedObject.ApplyModifiedProperties();
                evt.Use();
            }
        }

        #region Auto-detect Overrides (shared by all entry types)

        /// <summary>
        /// Auto-detect overrides for a flat array of AnimationEntryBase (or derived) entries.
        /// Works for OneShotMotionEntry, IdleAnimationEntry, etc.
        /// </summary>
        private void AutoDetectOverridesForArray(SerializedProperty arrayProp)
        {
            if (arrayProp == null) return;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                AutoDetectOverridesForEntry(arrayProp.GetArrayElementAtIndex(i));
            }
        }

        /// <summary>
        /// Auto-detect overrides for group arrays (entries nested inside groups).
        /// </summary>
        private void AutoDetectOverridesForGroupArray(SerializedProperty groupsProp)
        {
            if (groupsProp == null) return;
            for (int i = 0; i < groupsProp.arraySize; i++)
            {
                var entries = groupsProp.GetArrayElementAtIndex(i).FindPropertyRelative("entries");
                if (entries == null) continue;
                for (int j = 0; j < entries.arraySize; j++)
                {
                    AutoDetectOverridesForEntry(entries.GetArrayElementAtIndex(j));
                }
            }
        }

        /// <summary>
        /// Auto-detect overrideEyeControl and overrideEyeBlink for a single AnimationEntryBase entry.
        /// Triggered when clip reference changes.
        /// </summary>
        private void AutoDetectOverridesForEntry(SerializedProperty entryProp)
        {
            if (entryProp == null) return;

            var clipProp = entryProp.FindPropertyRelative("clip");
            if (clipProp == null) return;

            var clip = clipProp.objectReferenceValue as AnimationClip;
            int currentId = clip != null ? clip.GetInstanceID() : 0;
            string key = clipProp.propertyPath;

            if (previousClipInstanceIds.TryGetValue(key, out int prevId))
            {
                if (prevId != currentId && clip != null)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);

                    // Auto-detect eye control curves
                    var overrideEyeProp = entryProp.FindPropertyRelative("overrideEyeControl");
                    if (overrideEyeProp != null)
                    {
                        bool hasEyeCurves = bindings.Any(b =>
                            EYE_BLEND_SHAPE_PREFIXES.Any(prefix => b.propertyName.StartsWith(prefix)));
                        overrideEyeProp.boolValue = hasEyeCurves;
                    }

                    // Auto-detect eye blink curves
                    var overrideBlinkProp = entryProp.FindPropertyRelative("overrideEyeBlink");
                    if (overrideBlinkProp != null)
                    {
                        bool hasBlinkCurves = bindings.Any(b =>
                            EYE_BLINK_PREFIXES.Any(prefix => b.propertyName.StartsWith(prefix)));
                        overrideBlinkProp.boolValue = hasBlinkCurves;
                    }
                }
            }

            previousClipInstanceIds[key] = currentId;
        }

        #endregion

        #region Validation

        private void ValidateOneShotMotions(SerializedProperty motionsProp)
        {
            if (motionsProp == null || motionsProp.arraySize == 0)
                return;

            validationSeenIds.Clear();
            bool hasEmptyId = false;
            bool hasNullClip = false;

            for (int i = 0; i < motionsProp.arraySize; i++)
            {
                var element = motionsProp.GetArrayElementAtIndex(i);
                var motionId = element.FindPropertyRelative("motionId").stringValue;
                var clip = element.FindPropertyRelative("clip").objectReferenceValue;

                if (string.IsNullOrEmpty(motionId))
                {
                    hasEmptyId = true;
                }
                else if (validationSeenIds.ContainsKey(motionId))
                {
                    EditorGUILayout.HelpBox(
                        $"Duplicate Motion ID detected: \"{motionId}\" (Element {validationSeenIds[motionId]} and {i})",
                        MessageType.Warning);
                }
                else
                {
                    validationSeenIds[motionId] = i;
                }

                if (clip == null)
                {
                    hasNullClip = true;
                }
            }

            if (hasEmptyId)
            {
                EditorGUILayout.HelpBox("One or more entries have an empty Motion ID.", MessageType.Warning);
            }

            if (hasNullClip)
            {
                EditorGUILayout.HelpBox("One or more entries have no AnimationClip assigned.", MessageType.Warning);
            }
        }

        private void ValidateOneShotMotionGroups(SerializedProperty groupsProp)
        {
            if (groupsProp == null || groupsProp.arraySize == 0)
                return;

            validationSeenIds.Clear();
            bool hasEmptyId = false;

            for (int i = 0; i < groupsProp.arraySize; i++)
            {
                var element = groupsProp.GetArrayElementAtIndex(i);
                var groupId = element.FindPropertyRelative("groupId").stringValue;
                var entries = element.FindPropertyRelative("entries");

                if (string.IsNullOrEmpty(groupId))
                {
                    hasEmptyId = true;
                }
                else if (validationSeenIds.ContainsKey(groupId))
                {
                    EditorGUILayout.HelpBox(
                        $"Duplicate Group ID detected: \"{groupId}\" (Element {validationSeenIds[groupId]} and {i})",
                        MessageType.Warning);
                }
                else
                {
                    validationSeenIds[groupId] = i;
                }

                if (entries != null && entries.arraySize == 0 && !string.IsNullOrEmpty(groupId))
                {
                    EditorGUILayout.HelpBox(
                        $"Group \"{groupId}\" has no entries.",
                        MessageType.Warning);
                }
            }

            if (hasEmptyId)
            {
                EditorGUILayout.HelpBox("One or more groups have an empty Group ID.", MessageType.Warning);
            }
        }

        private string[] BuildMotionIdList(SerializedProperty motionsProp)
        {
            tempIdList.Clear();
            for (int i = 0; i < motionsProp.arraySize; i++)
            {
                var element = motionsProp.GetArrayElementAtIndex(i);
                var motionId = element.FindPropertyRelative("motionId").stringValue;
                var clip = element.FindPropertyRelative("clip").objectReferenceValue;
                if (!string.IsNullOrEmpty(motionId) && clip != null)
                    tempIdList.Add(motionId);
            }
            return tempIdList.ToArray();
        }

        private string[] BuildGroupIdList(SerializedProperty groupsProp)
        {
            tempIdList.Clear();
            for (int i = 0; i < groupsProp.arraySize; i++)
            {
                var element = groupsProp.GetArrayElementAtIndex(i);
                var groupId = element.FindPropertyRelative("groupId").stringValue;
                if (!string.IsNullOrEmpty(groupId))
                    tempIdList.Add(groupId);
            }
            return tempIdList.ToArray();
        }

        #endregion
    }
}
