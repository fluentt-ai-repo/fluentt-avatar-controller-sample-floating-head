using UnityEngine;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// StateMachineBehaviour that notifies FluentTAvatarControllerFloatingHead
    /// when a swap-buffer state becomes fully active (transition completed).
    ///
    /// Attach to each Idle 0/1 and Talking 0/1 state in the Animator Controller.
    /// When this state enters and the entry transition settles, it tells the controller
    /// to swap the OTHER slot's clip — well before the next ExitTime transition starts.
    /// </summary>
    public class SwapBufferNotifier : StateMachineBehaviour
    {
        public enum BufferGroup { Idle, Talking }

        [Tooltip("Which swap buffer group this state belongs to")]
        public BufferGroup group;

        [Tooltip("Which slot this state represents (0 or 1)")]
        public int slotIndex;

        private bool pendingSwap;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            pendingSwap = true;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (!pendingSwap)
                return;

            // Wait until transition is fully complete — the other slot is now completely dormant
            if (animator.IsInTransition(layerIndex))
                return;

            pendingSwap = false;

            var controller = animator.GetComponent<FluentTAvatarControllerFloatingHead>();
            if (controller != null)
            {
                controller.OnSwapSlotReady(group, slotIndex);
            }
        }
    }
}
