using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    [CustomEditor(typeof(FluentTAvatarControllerFloatingHead))]
    public partial class FluentTAvatarControllerFloatingHeadEditor : UnityEditor.Editor
    {
        private string[] _tabNames = { "Default Animation", "Look Target", "Emotion Tagging", "Server Motion Tagging", "Eye Blink" };

        // Reflection binding flags constant
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        // Log prefix constant
        private const string LogPrefix = "[FluentTAvatarControllerFloatingHead]";

        private string SessionStateKey => $"FluentTAvatarControllerFloatingHead_SelectedTab_{target.GetInstanceID()}";

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
                case 0: // Default Animation
                    DrawDefaultAnimationSettings();
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

        #endregion
    }
}
