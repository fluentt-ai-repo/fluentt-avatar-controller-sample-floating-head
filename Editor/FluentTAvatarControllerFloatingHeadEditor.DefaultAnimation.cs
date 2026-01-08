using UnityEditor;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        private void DrawDefaultAnimationSettings()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatar"),
                new GUIContent("Avatar", "FluentTAvatar component reference"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorController"),
                new GUIContent("Animator Controller", "Runtime Animator Controller for body animations (required for Default Idle and Server Motion Tagging)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Idle Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Default Idle Animation Clip Override\n\n" +
                "How it works:\n" +
                "1. The Animator Controller has a dummy state called 'default_dummy' that plays continuously\n" +
                "2. By assigning an AnimationClip here, you can override 'default_dummy' with your own idle pose + facial expression\n" +
                "3. This animation will play as the default state when no other animations are active\n\n" +
                "Requirements:\n" +
                "• Animator Controller must be assigned above\n" +
                "• Your animation clip should contain both body pose and facial expression (blend shapes)\n" +
                "• The animation should be loopable for seamless playback\n" +
                "• Bone hierarchy paths in the clip must match your avatar's hierarchy",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultIdleAnimationClip"),
                new GUIContent("Default Idle Animation", "Animation clip to override default_dummy state"));
        }
    }
}
