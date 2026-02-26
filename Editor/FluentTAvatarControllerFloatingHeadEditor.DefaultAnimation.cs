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
                "Multi-Idle Animation System\n\n" +
                "How it works:\n" +
                "1. Add one or more idle animation clips with weights below\n" +
                "2. At runtime, clips are selected by weighted random and played in sequence\n" +
                "3. When one clip finishes, the Animator cross-fades (0.3s) to the next randomly selected clip\n" +
                "4. Enable 'Prevent Repeat' on a clip to avoid it playing twice in a row\n\n" +
                "Requirements:\n" +
                "• Animator Controller must be assigned above\n" +
                "• Animation clips should contain body pose and/or facial expression (blend shapes)\n" +
                "• Clips should be loopable for seamless playback\n" +
                "• Bone hierarchy paths in clips must match your avatar's hierarchy\n\n" +
                "Note: If only 1 clip is assigned, it loops continuously (same as legacy behavior).",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("idleAnimations"),
                new GUIContent("Idle Animations", "List of idle animation clips with weights for random selection"), true);

            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Talking Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Talking Body Animation System\n\n" +
                "How it works:\n" +
                "1. Add one or more talking animation clips below\n" +
                "2. When speech starts (OnSentenceStarted), the Animator cross-fades from Idle to Talking\n" +
                "3. When speech ends (last sentence), it cross-fades back to Idle\n" +
                "4. Multiple clips use the same swap-buffer pattern as Idle (weighted random, ExitTime cross-fade)\n\n" +
                "If no talking animations are assigned, the avatar stays in Idle during speech (backward compatible).",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("talkingAnimations"),
                new GUIContent("Talking Animations", "List of talking body animation clips with weights for random selection"), true);
        }
    }
}
