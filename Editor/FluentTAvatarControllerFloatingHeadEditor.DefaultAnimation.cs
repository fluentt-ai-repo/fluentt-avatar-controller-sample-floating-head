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
        };

        // Cached GUIContent - Default Animation
        private static readonly GUIContent gc_avatar = new("Avatar", "FluentTAvatar component reference");
        private static readonly GUIContent gc_animController = new("Animator Controller", "Runtime Animator Controller for body animations (required for Default Idle and Server Motion Tagging)");
        private static readonly GUIContent gc_idleAnims = new("Idle Animations", "List of idle animation clips with weights for random selection");
        private static readonly GUIContent gc_talkMotionIdleClip = new("TalkMotion Idle Clip", "Idle clip played during TalkMotion speech (e.g. base expression with no body motion). Requires idleSwap trigger in Animator Controller.");

        // SwapBuffer check cache (always invalidated on selection change via OnEnable → -1)
        private int cachedSwapBufferMissing = -1;
        private Object cachedSwapBufferController = null;

        private void DrawDefaultAnimationSettings()
        {
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(avatarProp, gc_avatar);
            EditorGUILayout.PropertyField(animatorControllerProp, gc_animController);

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

            EditorGUILayout.PropertyField(idleAnimationsProp, gc_idleAnims, true);
            DrawAnimationClipDropArea(idleAnimationsProp, "Drop Idle AnimationClips here");
            AutoDetectOverridesForArray(idleAnimationsProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("TalkMotion Idle", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(talkMotionIdleClipProp, gc_talkMotionIdleClip);
            EditorGUILayout.HelpBox(
                "When TalkMotion starts, the Animator transitions to this clip via idleSwap trigger. " +
                "Use a base expression idle (no body motion) to avoid conflicts with server head animation. " +
                "Requires 'Setup SwapBuffer Behaviours' above (adds idleSwap trigger + transitions).",
                MessageType.None);
        }

        #region SwapBuffer Auto-Setup

        /// <summary>
        /// Draw the SwapBufferNotifier auto-setup section.
        /// Checks if the assigned AnimatorController has the required behaviours
        /// and provides a button to auto-attach them.
        /// </summary>
        private void DrawSwapBufferSetup()
        {
            var controllerRef = animatorControllerProp.objectReferenceValue as AnimatorController;
            if (controllerRef == null)
                return;

            // Recount on every draw (cheap check, avoids stale cache after setup)
            cachedSwapBufferMissing = CountMissingSwapBufferNotifiers(controllerRef);
            cachedSwapBufferController = controllerRef;

            int missingCount = cachedSwapBufferMissing;
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
                    cachedSwapBufferMissing = -1; // Invalidate cache
                }
            }
        }

        /// <summary>
        /// Count how many swap-buffer states are missing the SwapBufferNotifier behaviour.
        /// </summary>
        private const string IDLE_SWAP_TRIGGER = "idleSwap";
        private const float IDLE_SWAP_TRANSITION_DURATION = 0.3f;

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

            // Also check if idleSwap trigger is missing
            if (!HasIdleSwapTrigger(controller))
                missing++;

            return missing;
        }

        private bool HasIdleSwapTrigger(AnimatorController controller)
        {
            foreach (var param in controller.parameters)
            {
                if (param.name == IDLE_SWAP_TRIGGER && param.type == AnimatorControllerParameterType.Trigger)
                    return true;
            }
            return false;
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

            // Ensure idleSwap trigger parameter and transitions
            EnsureIdleSwapTrigger(controller, baseLayer.stateMachine);

            if (attached > 0)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Debug.Log($"{LogPrefix} SwapBuffer setup complete. {attached} behaviour(s) attached.");
            }
        }

        /// <summary>
        /// Ensure idleSwap trigger parameter and trigger-based transitions
        /// between Idle 0 ↔ Idle 1 exist.
        /// </summary>
        private void EnsureIdleSwapTrigger(AnimatorController controller, AnimatorStateMachine stateMachine)
        {
            // Add trigger parameter if not present
            if (!HasIdleSwapTrigger(controller))
            {
                controller.AddParameter(IDLE_SWAP_TRIGGER, AnimatorControllerParameterType.Trigger);
                Debug.Log($"{LogPrefix} Added '{IDLE_SWAP_TRIGGER}' trigger parameter");
            }

            AnimatorState idle0 = FindStateByName(stateMachine, "Idle 0");
            AnimatorState idle1 = FindStateByName(stateMachine, "Idle 1");
            if (idle0 == null || idle1 == null)
                return;

            EnsureIdleSwapTransition(idle0, idle1);
            EnsureIdleSwapTransition(idle1, idle0);

            EditorUtility.SetDirty(controller);
        }

        private void EnsureIdleSwapTransition(AnimatorState source, AnimatorState destination)
        {
            foreach (var transition in source.transitions)
            {
                if (transition.destinationState == destination && !transition.hasExitTime)
                {
                    foreach (var condition in transition.conditions)
                    {
                        if (condition.parameter == IDLE_SWAP_TRIGGER)
                            return; // Already exists
                    }
                }
            }

            var newTransition = source.AddTransition(destination);
            newTransition.hasExitTime = false;
            newTransition.duration = IDLE_SWAP_TRANSITION_DURATION;
            newTransition.hasFixedDuration = true;
            newTransition.AddCondition(AnimatorConditionMode.If, 0, IDLE_SWAP_TRIGGER);

            Debug.Log($"{LogPrefix} Added idleSwap transition: {source.name} → {destination.name}");
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
