using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// State machine behavior that randomly selects and plays talking animations
    /// Attach this to talking1 and talking2 states in the Animator
    /// </summary>
    public class TalkingAnimationRandomizer : StateMachineBehaviour
    {
        [HideInInspector]
        [Tooltip("Reference to the controller that provides animation clips (set automatically at runtime)")]
        public FluentTAvatarSampleController controller;

        [Tooltip("Dummy clip name to override (e.g., 'talking_dummy_0' or 'talking_dummy_1')")]
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
                controller = animator.GetComponent<FluentTAvatarSampleController>();
                if (controller == null)
                {
                    if (!hasShownError)
                    {
                        Debug.LogError("[TalkingAnimationRandomizer] Controller not found! Make sure FluentTAvatarSampleController is attached to the same GameObject as the Animator. This behaviour will be disabled.");
                        hasShownError = true;
                        isDisabled = true; // Disable this behaviour
                    }
                    return;
                }
            }

            if (overrideController == null)
            {
                Debug.LogError("[TalkingAnimationRandomizer] Override controller not initialized!");
                return;
            }

            // Get talking clips from controller
            List<AnimationClip> talkingClips = controller.talkingClips;
            if (talkingClips == null || talkingClips.Count == 0)
            {
                Debug.LogWarning("[TalkingAnimationRandomizer] No talking clips available!");
                return;
            }

            // Select random clip (avoid repeating the same clip if possible)
            AnimationClip selectedClip;
            if (talkingClips.Count == 1)
            {
                // Only one clip available - use it
                selectedClip = talkingClips[0];
            }
            else
            {
                // Multiple clips available - avoid the last played clip
                List<AnimationClip> availableClips = talkingClips.Where(clip => clip != controller.lastTalkingClip).ToList();
                if (availableClips.Count == 0)
                {
                    // Fallback: all clips are the same as last (shouldn't happen)
                    selectedClip = talkingClips[Random.Range(0, talkingClips.Count)];
                }
                else
                {
                    selectedClip = availableClips[Random.Range(0, availableClips.Count)];
                }
            }

            // Remember this clip for next time
            controller.lastTalkingClip = selectedClip;

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
                Debug.LogError("[TalkingAnimationRandomizer] Animator controller is not an AnimatorOverrideController!");
            }

            initialized = true;
        }
    }
}
