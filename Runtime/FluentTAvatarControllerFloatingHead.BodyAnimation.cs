using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Body Animation control partial class
    /// Handles animator setup and layer-based body animation control
    ///
    /// Animation Structure:
    /// - Layer 0 (Idle): idle1 <-> idle2 (continuous loop)
    /// - Layer 1 (Talking): talking1 <-> talking2 (continuous loop)
    ///
    /// Layer 1 weight is controlled by talking state:
    /// - Talking starts: weight 0 -> 1 (smooth blend)
    /// - Talking ends: weight 1 -> 0 (smooth blend)
    ///
    /// This prevents abrupt state transitions when talking starts/stops mid-animation.
    /// </summary>
    public partial class FluentTAvatarControllerFloatingHead
    {
        #region Body Animation Initialization

        private void InitializeAnimator()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] No Animator component found");
                return;
            }

            // Set animator controller
            if (animatorController != null)
            {
                // Create override controller for dummy clip replacement
                overrideController = new AnimatorOverrideController(animatorController);
                animator.runtimeAnimatorController = overrideController;
            }
            else
            {
                Debug.LogWarning("[FluentTAvatarControllerFloatingHead] No animator controller assigned");
                return;
            }

            // Initialize talking layer weight to 0
            if (animator.layerCount >= 2)
            {
                animator.SetLayerWeight(1, 0f);
                currentTalkingLayerWeight = 0f;
                targetTalkingLayerWeight = 0f;
            }

            // Setup state machine behaviours
            SetupStateMachineBehaviours();

            Debug.Log("[FluentTAvatarControllerFloatingHead] Animator initialized successfully");
        }

        /// <summary>
        /// Setup state machine behaviours to reference this controller
        /// </summary>
        private void SetupStateMachineBehaviours()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            // Get all state machine behaviours
            var behaviours = animator.GetBehaviours<IdleAnimationRandomizer>();
            foreach (var behaviour in behaviours)
            {
                behaviour.controller = this;
            }

            var talkingBehaviours = animator.GetBehaviours<TalkingAnimationRandomizer>();
            foreach (var behaviour in talkingBehaviours)
            {
                behaviour.controller = this;
            }

            Debug.Log($"[FluentTAvatarControllerFloatingHead] Setup {behaviours.Length} idle and {talkingBehaviours.Length} talking state behaviours");
        }

        #endregion

        #region Animation State Control

        /// <summary>
        /// Start talking animations by increasing layer weight
        /// No state transitions - just blend in talking layer
        /// </summary>
        private void StartTalkingAnimations(AudioClip audioClip)
        {
            if (animator != null && talkingClips.Count > 0 && animator.layerCount >= 2)
            {
                targetTalkingLayerWeight = 1f;
            }
        }

        /// <summary>
        /// Stop talking animations by decreasing layer weight
        /// No state transitions - just blend out talking layer
        /// </summary>
        private void StopTalkingAnimations(AudioClip audioClip)
        {
            if (animator != null && animator.layerCount >= 2)
            {
                targetTalkingLayerWeight = 0f;
            }
        }

        #endregion
    }
}
