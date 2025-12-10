using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// State machine behavior that randomly selects and plays idle animations
    /// Attach this to idle1 and idle2 states in the Animator
    /// </summary>
    public class IdleAnimationRandomizer : StateMachineBehaviour
    {
        [HideInInspector]
        [Tooltip("Reference to the controller that provides animation clips (set automatically at runtime)")]
        public FluentTAvatarControllerFloatingHead controller;

        [Tooltip("Dummy clip name to override (e.g., 'idle_dummy_0' or 'idle_dummy_1')")]
        public string dummyClipName;

        private AnimatorOverrideController overrideController;
        private bool initialized = false;
        private bool hasShownError = false;
        private bool isDisabled = false;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // Early return if disabled due to previous error
            if (isDisabled)
                return;

            if (!initialized)
            {
                Initialize(animator);
            }

            // Auto-find controller if null
            if (controller == null)
            {
                controller = animator.GetComponent<FluentTAvatarControllerFloatingHead>();
                if (controller == null)
                {
                    if (!hasShownError)
                    {
                        Debug.LogError("[IdleAnimationRandomizer] Controller not found! Make sure FluentTAvatarControllerFloatingHead is attached to the same GameObject as the Animator. This behaviour will be disabled.");
                        hasShownError = true;
                        isDisabled = true; // Disable this behaviour
                    }
                    return;
                }
                else
                {
                    Debug.Log("[IdleAnimationRandomizer] Controller auto-found!");
                }
            }

            if (overrideController == null)
            {
                Debug.LogError("[IdleAnimationRandomizer] Override controller not initialized!");
                return;
            }

            // Get idle clips from controller
            List<AnimationClip> idleClips = controller.idleClips;
            if (idleClips == null || idleClips.Count == 0)
            {
                Debug.LogWarning("[IdleAnimationRandomizer] No idle clips available!");
                return;
            }

            // Select random clip (avoid repeating the same clip if possible)
            AnimationClip selectedClip;
            if (idleClips.Count == 1)
            {
                // Only one clip available - use it
                selectedClip = idleClips[0];
            }
            else
            {
                // Multiple clips available - avoid the last played clip
                List<AnimationClip> availableClips = idleClips.Where(clip => clip != controller.lastIdleClip).ToList();
                if (availableClips.Count == 0)
                {
                    // Fallback: all clips are the same as last (shouldn't happen)
                    selectedClip = idleClips[Random.Range(0, idleClips.Count)];
                }
                else
                {
                    selectedClip = availableClips[Random.Range(0, availableClips.Count)];
                }
            }

            // Remember this clip for next time
            controller.lastIdleClip = selectedClip;

            // Override dummy clip
            overrideController[dummyClipName] = selectedClip;
        }

        private void Initialize(Animator animator)
        {
            if (animator.runtimeAnimatorController is AnimatorOverrideController existing)
            {
                overrideController = existing;
            }
            else
            {
                Debug.LogError("[IdleAnimationRandomizer] Animator controller is not an AnimatorOverrideController!");
            }

            initialized = true;
        }
    }
}
