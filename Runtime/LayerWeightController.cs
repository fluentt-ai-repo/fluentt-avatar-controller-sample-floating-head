using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// StateMachineBehaviour that drives an Animator layer's weight
    /// toward a target value while the attached state is active.
    ///
    /// Usage: attach to each state in the Motion Tagging layer.
    /// - Emotion states: activeWeight=1, fadeDuration=0.1
    /// - Dummy/idle states: activeWeight=0, fadeDuration=0.2
    /// </summary>
    public class LayerWeightController : StateMachineBehaviour
    {
        [Tooltip("Target layer index to control. -1 = the layer this state belongs to.")]
        public int targetLayerIndex = -1;

        [Tooltip("Target layer weight while this state is active")]
        [Range(0f, 1f)]
        public float activeWeight = 1f;

        [Tooltip("Time to reach target weight (seconds). 0 = instant.")]
        public float fadeDuration = 0.1f;

        private int resolvedLayerIndex;
        private float startWeight;
        private float elapsed;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            resolvedLayerIndex = targetLayerIndex >= 0 ? targetLayerIndex : layerIndex;
            startWeight = animator.GetLayerWeight(resolvedLayerIndex);
            elapsed = 0f;

            if (fadeDuration <= 0f)
            {
                animator.SetLayerWeight(resolvedLayerIndex, activeWeight);
            }
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (fadeDuration <= 0f)
                return;

            float currentWeight = animator.GetLayerWeight(resolvedLayerIndex);
            if (Mathf.Approximately(currentWeight, activeWeight))
                return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            animator.SetLayerWeight(resolvedLayerIndex, Mathf.Lerp(startWeight, activeWeight, t));
        }
    }
}
