using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead.Editor
{
    public partial class FluentTAvatarControllerFloatingHeadEditor
    {
        // State names in the Animator Controller Base layer
        private static readonly (string name, SwapBufferNotifier.BufferGroup group, int slot)[] swapBufferStates =
        {
            ("Idle 0", SwapBufferNotifier.BufferGroup.Idle, 0),
            ("Idle 1", SwapBufferNotifier.BufferGroup.Idle, 1),
            ("Talking 0", SwapBufferNotifier.BufferGroup.Talking, 0),
            ("Talking 1", SwapBufferNotifier.BufferGroup.Talking, 1),
        };

        private void DrawDefaultAnimationSettings()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("avatar"),
                new GUIContent("Avatar", "FluentTAvatar component reference"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("animatorController"),
                new GUIContent("Animator Controller", "Runtime Animator Controller for body animations (required for Default Idle and Server Motion Tagging)"));

            // Auto-setup button for SwapBufferNotifier behaviours
            DrawSwapBufferSetup();

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

            // Energy Matching settings
            EditorGUILayout.Space();
            var enableEnergyMatchingProp = serializedObject.FindProperty("enableEnergyMatching");
            EditorGUILayout.PropertyField(enableEnergyMatchingProp,
                new GUIContent("Enable Energy Matching", "Select talking clips by audio/motion energy similarity instead of weighted random"));

            if (enableEnergyMatchingProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("energyBlendRatio"),
                    new GUIContent("Energy Blend Ratio", "0 = weight only, 1 = similarity only. Controls how much energy similarity affects clip selection."));
                EditorGUILayout.HelpBox(
                    "Energy Matching compares the audio RMS curve of each sentence with precomputed motion energy curves " +
                    "to automatically select the best-matching talking animation.\n\n" +
                    "Final Score = (1 - ratio) × weight + ratio × similarity\n\n" +
                    "Requires 2+ talking animation clips. Falls back to weighted random when disabled or AudioClip is null.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
        }

        #region SwapBuffer Auto-Setup

        /// <summary>
        /// Draw the SwapBufferNotifier auto-setup section.
        /// Checks if the assigned AnimatorController has the required behaviours
        /// and provides a button to auto-attach them.
        /// </summary>
        private void DrawSwapBufferSetup()
        {
            var controllerProp = serializedObject.FindProperty("animatorController");
            var controllerRef = controllerProp.objectReferenceValue as AnimatorController;
            if (controllerRef == null)
                return;

            // Check if setup is needed
            int missingCount = CountMissingSwapBufferNotifiers(controllerRef);
            if (missingCount > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $"SwapBufferNotifier is missing on {missingCount} state(s). " +
                    "Click below to auto-attach for smooth clip swapping.",
                    MessageType.Warning);

                if (GUILayout.Button("Setup SwapBuffer Behaviours"))
                {
                    AttachSwapBufferNotifiers(controllerRef);
                }
            }
        }

        /// <summary>
        /// Count how many swap-buffer states are missing the SwapBufferNotifier behaviour.
        /// </summary>
        private int CountMissingSwapBufferNotifiers(AnimatorController controller)
        {
            if (controller.layers.Length == 0)
                return 0;

            var baseLayer = controller.layers[0];
            int missing = 0;

            for (int s = 0; s < swapBufferStates.Length; s++)
            {
                string stateName = swapBufferStates[s].name;
                AnimatorState state = FindStateByName(baseLayer.stateMachine, stateName);
                if (state == null)
                    continue;

                if (!HasSwapBufferNotifier(state))
                    missing++;
            }

            return missing;
        }

        /// <summary>
        /// Auto-attach SwapBufferNotifier behaviours to all swap-buffer states.
        /// </summary>
        private void AttachSwapBufferNotifiers(AnimatorController controller)
        {
            if (controller.layers.Length == 0)
                return;

            var baseLayer = controller.layers[0];
            int attached = 0;

            for (int s = 0; s < swapBufferStates.Length; s++)
            {
                string stateName = swapBufferStates[s].name;
                var group = swapBufferStates[s].group;
                int slot = swapBufferStates[s].slot;

                AnimatorState state = FindStateByName(baseLayer.stateMachine, stateName);
                if (state == null)
                {
                    Debug.LogWarning($"{LogPrefix} State '{stateName}' not found in Base layer. Skipping.");
                    continue;
                }

                if (HasSwapBufferNotifier(state))
                    continue;

                var behaviour = state.AddStateMachineBehaviour<SwapBufferNotifier>();
                behaviour.group = group;
                behaviour.slotIndex = slot;
                attached++;

                Debug.Log($"{LogPrefix} Attached SwapBufferNotifier to '{stateName}' (group={group}, slot={slot})");
            }

            if (attached > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log($"{LogPrefix} SwapBuffer setup complete. {attached} behaviour(s) attached.");
            }
        }

        /// <summary>
        /// Find an AnimatorState by name in a state machine.
        /// </summary>
        private AnimatorState FindStateByName(AnimatorStateMachine stateMachine, string name)
        {
            var states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == name)
                    return states[i].state;
            }
            return null;
        }

        /// <summary>
        /// Check if a state already has SwapBufferNotifier attached.
        /// </summary>
        private bool HasSwapBufferNotifier(AnimatorState state)
        {
            var behaviours = state.behaviours;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is SwapBufferNotifier)
                    return true;
            }
            return false;
        }

        #endregion
    }
}
