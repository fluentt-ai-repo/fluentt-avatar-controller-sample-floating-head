using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    [CustomEditor(typeof(FluentTAvatarControllerFloatingHead))]
    public partial class FluentTAvatarControllerFloatingHeadEditor : UnityEditor.Editor
    {
        // Accordion section indices
        private const int SECTION_DEFAULT_ANIMATION = 0;
        private const int SECTION_LOOK_TARGET = 1;
        private const int SECTION_TEXT_EMOTION = 2;
        private const int SECTION_GESTURE = 3;
        private const int SECTION_EYE_BLINK = 4;
        private const int SECTION_ONESHOT_MOTION = 5;

        private int expandedSectionThisFrame;

        // lilToon-style foldout header (based on ShurikenModuleTitle)
        private static GUIStyle sectionHeaderStyle;
        private static bool stylesInitialized;

        // Reflection binding flags constant
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        // Log prefix constant
        private const string LogPrefix = "[FluentTAvatarControllerFloatingHead]";

        private string SessionStateKey => $"FluentTAvatarControllerFloatingHead_SelectedTab_{target.GetInstanceID()}";

        // Cached SerializedProperties (initialized in OnEnable)
        // -- Default Animation
        private SerializedProperty avatarProp;
        private SerializedProperty animatorControllerProp;
        private SerializedProperty idleAnimationsProp;
        private SerializedProperty talkMotionIdleClipProp;
        // -- Emotion Tagging
        private SerializedProperty enableTextEmotionDetectionProp;
        private SerializedProperty maxEmotionTagsPerSentenceProp;
        private SerializedProperty emotionKeywordDatasetProp;
        // -- Gesture Animation
        private SerializedProperty enableServerMotionTaggingProp;
        private SerializedProperty enableAutoEmotionResetProp;
        private SerializedProperty gestureMappingsProp;
        // -- Eye Blink
        private SerializedProperty enableEyeBlinkProp;
        private SerializedProperty blinkClipProp;
        private SerializedProperty blinkBlendModeProp;
        private SerializedProperty blinkIntervalProp;
        private SerializedProperty blinkIntervalVarianceProp;
        // -- One-Shot Motion
        private SerializedProperty oneShotMotionsProp;
        private SerializedProperty oneShotMotionGroupsProp;
        private SerializedProperty onOneShotMotionStartedProp;
        private SerializedProperty onOneShotMotionEndedProp;
        // -- Look Target
        private SerializedProperty enableLookTargetProp;
        private SerializedProperty lookTargetProp;
        private SerializedProperty headSkinnedMeshRenderersProp;
        private SerializedProperty lookHeadProp;
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
        private SerializedProperty headAimConstraintProp;
        private SerializedProperty leftEyeAimConstraintProp;
        private SerializedProperty rightEyeAimConstraintProp;
#endif
        private SerializedProperty lookLeftEyeBallProp;
        private SerializedProperty lookRightEyeBallProp;
        private SerializedProperty idleLookSettingsProp;
        private SerializedProperty talkingLookSettingsProp;
        private SerializedProperty enableHeadControlProp;
        private SerializedProperty headSpeedProp;
        private SerializedProperty enableEyeControlProp;
        private SerializedProperty eyeControlStrategyProp;
        private SerializedProperty eyeBlendShapesProp;
        private SerializedProperty eyeAngleLimitProp;
        private SerializedProperty eyeAngleLimitThresholdProp;
        private SerializedProperty eyeSpeedProp;
        private SerializedProperty showTargetGizmosProp;
        private SerializedProperty actualTargetGizmoSizeProp;
        private SerializedProperty headVirtualTargetGizmoSizeProp;
        private SerializedProperty eyeVirtualTargetGizmoSizeProp;
        private SerializedProperty actualTargetColorProp;
        private SerializedProperty headVirtualTargetColorProp;
        private SerializedProperty eyeVirtualTargetColorProp;

        // Logging
        private SerializedProperty enableVerboseLoggingProp;

        private void OnEnable()
        {
            if (target == null || serializedObject == null) return;

            // Logging
            enableVerboseLoggingProp = serializedObject.FindProperty("enableVerboseLogging");

            // Default Animation
            avatarProp = serializedObject.FindProperty("avatar");
            animatorControllerProp = serializedObject.FindProperty("animatorController");
            idleAnimationsProp = serializedObject.FindProperty("idleAnimations");
            talkMotionIdleClipProp = serializedObject.FindProperty("talkMotionIdleClip");
            // Emotion Tagging
            enableTextEmotionDetectionProp = serializedObject.FindProperty("enableTextEmotionDetection");
            maxEmotionTagsPerSentenceProp = serializedObject.FindProperty("maxEmotionTagsPerSentence");
            emotionKeywordDatasetProp = serializedObject.FindProperty("emotionKeywordDataset");
            // Gesture Animation
            enableServerMotionTaggingProp = serializedObject.FindProperty("enableServerMotionTagging");
            enableAutoEmotionResetProp = serializedObject.FindProperty("enableAutoEmotionReset");
            gestureMappingsProp = serializedObject.FindProperty("gestureMappings");
            // Eye Blink
            enableEyeBlinkProp = serializedObject.FindProperty("enableEyeBlink");
            blinkClipProp = serializedObject.FindProperty("blinkClip");
            blinkBlendModeProp = serializedObject.FindProperty("blinkBlendMode");
            blinkIntervalProp = serializedObject.FindProperty("blinkInterval");
            blinkIntervalVarianceProp = serializedObject.FindProperty("blinkIntervalVariance");
            // One-Shot Motion
            oneShotMotionsProp = serializedObject.FindProperty("oneShotMotions");
            oneShotMotionGroupsProp = serializedObject.FindProperty("oneShotMotionGroups");
            onOneShotMotionStartedProp = serializedObject.FindProperty("onOneShotMotionStarted");
            onOneShotMotionEndedProp = serializedObject.FindProperty("onOneShotMotionEnded");
            // Look Target
            enableLookTargetProp = serializedObject.FindProperty("enableLookTarget");
            lookTargetProp = serializedObject.FindProperty("lookTarget");
            headSkinnedMeshRenderersProp = serializedObject.FindProperty("headSkinnedMeshRenderers");
            lookHeadProp = serializedObject.FindProperty("lookHead");
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
            headAimConstraintProp = serializedObject.FindProperty("headAimConstraint");
            leftEyeAimConstraintProp = serializedObject.FindProperty("leftEyeAimConstraint");
            rightEyeAimConstraintProp = serializedObject.FindProperty("rightEyeAimConstraint");
#endif
            lookLeftEyeBallProp = serializedObject.FindProperty("lookLeftEyeBall");
            lookRightEyeBallProp = serializedObject.FindProperty("lookRightEyeBall");
            idleLookSettingsProp = serializedObject.FindProperty("idleLookSettings");
            talkingLookSettingsProp = serializedObject.FindProperty("talkingLookSettings");
            enableHeadControlProp = serializedObject.FindProperty("enableHeadControl");
            headSpeedProp = serializedObject.FindProperty("headSpeed");
            enableEyeControlProp = serializedObject.FindProperty("enableEyeControl");
            eyeControlStrategyProp = serializedObject.FindProperty("eyeControlStrategy");
            eyeBlendShapesProp = serializedObject.FindProperty("eyeBlendShapes");
            eyeAngleLimitProp = serializedObject.FindProperty("eyeAngleLimit");
            eyeAngleLimitThresholdProp = serializedObject.FindProperty("eyeAngleLimitThreshold");
            eyeSpeedProp = serializedObject.FindProperty("eyeSpeed");
            showTargetGizmosProp = serializedObject.FindProperty("showTargetGizmos");
            actualTargetGizmoSizeProp = serializedObject.FindProperty("actualTargetGizmoSize");
            headVirtualTargetGizmoSizeProp = serializedObject.FindProperty("headVirtualTargetGizmoSize");
            eyeVirtualTargetGizmoSizeProp = serializedObject.FindProperty("eyeVirtualTargetGizmoSize");
            actualTargetColorProp = serializedObject.FindProperty("actualTargetColor");
            headVirtualTargetColorProp = serializedObject.FindProperty("headVirtualTargetColor");
            eyeVirtualTargetColorProp = serializedObject.FindProperty("eyeVirtualTargetColor");
        }

        private static void InitializeStyles()
        {
            if (stylesInitialized) return;
            sectionHeaderStyle = new GUIStyle("ShurikenModuleTitle")
            {
                font = EditorStyles.label.font,
                fontSize = EditorStyles.label.fontSize,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(15, 7, 4, 4),
                contentOffset = new Vector2(20f, -2f),
                fixedHeight = 22
            };
            stylesInitialized = true;
        }

        private bool DrawAccordionHeader(string title, int sectionIndex)
        {
            InitializeStyles();
            bool isExpanded = expandedSectionThisFrame == sectionIndex;

            var rect = GUILayoutUtility.GetRect(16f, 20f, sectionHeaderStyle);
            rect.x -= 8f;
            rect.width += 8f;
            GUI.Box(rect, title, sectionHeaderStyle);

            // Draw foldout arrow
            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (Event.current.type == EventType.Repaint)
                EditorStyles.foldout.Draw(toggleRect, false, false, isExpanded, false);

            // Click handling
            rect.width -= 24;
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                expandedSectionThisFrame = isExpanded ? -1 : sectionIndex;
                Event.current.Use();
            }

            return expandedSectionThisFrame == sectionIndex;
        }

        public override void OnInspectorGUI()
        {
            if (serializedObject == null || target == null)
                return;

            serializedObject.Update();

            // Verbose Logging toggle at the top
            if (enableVerboseLoggingProp != null)
            {
                EditorGUILayout.PropertyField(enableVerboseLoggingProp, new GUIContent("Verbose Logging", "Enable detailed initialization logs. Disable for production builds."));
                EditorGUILayout.Space(4);
            }

            expandedSectionThisFrame = SessionState.GetInt(SessionStateKey, SECTION_DEFAULT_ANIMATION);

            if (DrawAccordionHeader("Default Animation", SECTION_DEFAULT_ANIMATION))
                DrawDefaultAnimationSettings();

            if (DrawAccordionHeader("Look Target", SECTION_LOOK_TARGET))
            {
#if FLUENTT_ANIMATION_RIGGING_AVAILABLE
                DrawLookTargetSettings();
#else
                EditorGUILayout.HelpBox(
                    "Look Target 기능을 사용하려면 Animation Rigging 패키지가 필요합니다.\n\n" +
                    "설치 방법: Window > Package Manager > Unity Registry에서 'Animation Rigging'을 검색하여 설치해 주세요.",
                    MessageType.Warning);
#endif
            }

            if (DrawAccordionHeader("Text Emotion Detection", SECTION_TEXT_EMOTION))
                DrawEmotionTaggingSettings();

            if (DrawAccordionHeader("Gesture Animation", SECTION_GESTURE))
                DrawGestureAnimationSettings();

            if (DrawAccordionHeader("Eye Blink", SECTION_EYE_BLINK))
                DrawEyeBlinkSettings();

            if (DrawAccordionHeader("One-Shot Motion", SECTION_ONESHOT_MOTION))
                DrawOneShotMotionSettings();

            SessionState.SetInt(SessionStateKey, expandedSectionThisFrame);
            serializedObject.ApplyModifiedProperties();
        }

        #region Helper Methods

        /// <summary>
        /// Get private field value using reflection
        /// </summary>
        private T GetFieldValue<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, PrivateInstance);
            return field != null ? (T)field.GetValue(obj) : default;
        }

        /// <summary>
        /// Set private field value using reflection
        /// </summary>
        private void SetFieldValue(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, PrivateInstance);
            field?.SetValue(obj, value);
        }

        /// <summary>
        /// Find or create VirtualTargets container in scene root
        /// </summary>
        private GameObject FindOrCreateVirtualTargetsContainer()
        {
            GameObject container = GameObject.Find("VirtualTargets");
            if (container == null)
            {
                container = new GameObject("VirtualTargets");
                Debug.Log($"{LogPrefix} Created VirtualTargets container");
            }
            return container;
        }

        /// <summary>
        /// Find or create avatar-specific virtual target group
        /// </summary>
        private Transform FindOrCreateAvatarVirtualTargetGroup(GameObject avatar)
        {
            GameObject container = FindOrCreateVirtualTargetsContainer();
            string groupName = $"{avatar.name}_VirtualTargets";
            Transform group = container.transform.Find(groupName);
            if (group == null)
            {
                GameObject groupGO = new GameObject(groupName);
                group = groupGO.transform;
                group.SetParent(container.transform);
                Debug.Log($"{LogPrefix} Created {groupName} group");
            }
            return group;
        }

        /// <summary>
        /// Find avatar virtual target group (returns null if not found)
        /// </summary>
        private Transform FindAvatarVirtualTargetGroup(GameObject avatar)
        {
            GameObject container = GameObject.Find("VirtualTargets");
            if (container == null)
                return null;
            return container.transform.Find($"{avatar.name}_VirtualTargets");
        }

        /// <summary>
        /// Draw a drag-and-drop area for AnimationClips.
        /// Dropped clips are wrapped in AnimationEntryBase entries and appended to the array.
        /// </summary>
        protected void DrawAnimationClipDropArea(SerializedProperty arrayProp, string label = "Drop AnimationClips here")
        {
            var dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, label, EditorStyles.helpBox);

            var evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated)
            {
                bool hasClip = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is AnimationClip)
                    {
                        hasClip = true;
                        break;
                    }
                }
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
                        newEntry.FindPropertyRelative("overrideEyeControl").boolValue = false;
                        newEntry.FindPropertyRelative("overrideEyeBlink").boolValue = false;

                        // Auto-detect overrides for the newly added clip
                        AutoDetectOverridesForEntry(newEntry);
                    }
                }
                serializedObject.ApplyModifiedProperties();
                evt.Use();
            }
        }

        #endregion
    }
}
