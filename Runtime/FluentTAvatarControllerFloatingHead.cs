using FluentT.Animation;
using FluentT.Talkmotion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

namespace FluentT.Avatar.SampleFloatingHead
{
    /// <summary>
    /// Sample controller component that demonstrates how to integrate FluentTAvatar with body animation and look target control.
    /// This component is NOT part of the core TalkMotion SDK - it's a reference implementation showing how to use
    /// FluentTAvatar callbacks to control animator and look target behaviors.
    ///
    /// Use this as a starting point for your own custom avatar control logic.
    /// </summary>
    [RequireComponent(typeof(FluentTAvatar))]
    public partial class FluentTAvatarControllerFloatingHead : MonoBehaviour
    {
        // References
        [SerializeField] private FluentTAvatar avatar;
        [SerializeField] private RuntimeAnimatorController animatorController;

        // Default Idle Animation
        [SerializeField] private List<IdleAnimationEntry> idleAnimations = new List<IdleAnimationEntry>();

        // Legacy field for auto-migration (hidden from Inspector)
        [HideInInspector] [SerializeField] private AnimationClip defaultIdleAnimationClip;

        // Look Target
        [SerializeField] private bool enableLookTarget = false;
        [SerializeField] private Transform lookTarget;

        // Animation Rigging Multi-Aim Constraints
        [SerializeField] private MultiAimConstraint headAimConstraint;
        [SerializeField] private MultiAimConstraint leftEyeAimConstraint;
        [SerializeField] private MultiAimConstraint rightEyeAimConstraint;

        // Look Target Transforms
        [SerializeField] private Transform lookHead;
        [SerializeField] private Transform lookLeftEyeBall;
        [SerializeField] private Transform lookRightEyeBall;
        [SerializeField] private LookTargetSetting idleLookSettings = new LookTargetSetting
        {
            headStrategy = ELookTargetStrategy.FocusedOnTarget,
            headLookIntoVoid = new Vector2(0, 0),
            headAngleVariance = new Vector2(0, 0),
            eyeStrategy = ELookTargetStrategy.FocusedOnTarget,
            eyeLookIntoVoid = new Vector2(0, 0),
            eyeAngleVariance = new Vector2(0, 0)
        };
        [SerializeField] private LookTargetSetting talkingLookSettings = new LookTargetSetting
        {
            headStrategy = ELookTargetStrategy.FocusedOnTarget,
            headLookIntoVoid = new Vector2(0, 0),
            headAngleVariance = new Vector2(0, 0),
            eyeStrategy = ELookTargetStrategy.FocusedOnTarget,
            eyeLookIntoVoid = new Vector2(0, 0),
            eyeAngleVariance = new Vector2(0, 0)
        };

        // Text Emotion Detection
        [FormerlySerializedAs("enableClientEmotionTagging")]
        [SerializeField] public bool enableTextEmotionDetection = false;
        [SerializeField] public int maxEmotionTagsPerSentence = 1;
        [SerializeField] public EmotionKeywordDataset emotionKeywordDataset;

        // Gesture Animation
        [SerializeField] public bool enableServerMotionTagging = false;
        [Tooltip("Automatically reset gesture animation to Idle when last sentence ends")]
        [SerializeField] public bool enableAutoEmotionReset = true;
        [FormerlySerializedAs("emotionMotionMappings")]
        [SerializeField] public List<GestureMapping> gestureMappings = new List<GestureMapping>();

        // Eye Blink
        [SerializeField] public bool enableEyeBlink = false;
        [Tooltip("Eye blink animation clip (if null, uses default ARKit eyeBlink animation)")]
        [SerializeField] public TMAnimationClip blinkClip = null;
        [Tooltip("How blink layer values combine with face animation values")]
        [SerializeField] public TMAnimationLayer.BlendMode blinkBlendMode = TMAnimationLayer.BlendMode.Override;
        [SerializeField] [Range(1f, 10f)] public float blinkInterval = 3f;
        [SerializeField] [Range(0f, 5f)] public float blinkIntervalVariance = 1f;

        // Private state
        private Animator animator;
        private AnimatorOverrideController overrideController;

        // Cached references
        [SerializeField] private List<SkinnedMeshRenderer> head_skmr;

        private void Reset()
        {
            // Auto-initialize references when component is added or reset
            if (avatar == null)
            {
                avatar = GetComponent<FluentTAvatar>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

#if UNITY_EDITOR
            // Auto-assign animator controller from package
            if (animatorController == null)
            {
                animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Packages/com.fluentt.avatar-controller-sample-floating-head/Animation/FluentTAvatarFloatingHeadController.controller");
            }

            // Auto-assign default idle animation if list is empty
            if (idleAnimations.Count == 0)
            {
                var idleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Packages/com.fluentt.avatar-controller-sample-floating-head/Animation/idle_sample.anim");
                if (idleClip != null)
                {
                    idleAnimations.Add(new IdleAnimationEntry { clip = idleClip, weight = 1f });
                }
            }
#endif

            // Auto-create default blink clip
            if (blinkClip == null)
            {
                blinkClip = CreateDefaultBlinkClip();
            }

            // Cache head SkinnedMeshRenderers that have blend shapes
            head_skmr = new List<SkinnedMeshRenderer>();

            // Check self first
            if (TryGetComponent<SkinnedMeshRenderer>(out var selfSkmr))
            {
                if (selfSkmr.sharedMesh != null && selfSkmr.sharedMesh.blendShapeCount > 0)
                {
                    head_skmr.Add(selfSkmr);
                }
            }

            // Then check children
            var skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var skmr in skinnedMeshRenderers)
            {
                if (skmr.sharedMesh != null && skmr.sharedMesh.blendShapeCount > 0)
                {
                    head_skmr.Add(skmr);
                }
            }

            // Auto-find look target transforms
            FindLookTargetTransforms();
        }

        private void Awake()
        {
            if (avatar == null)
            {
                avatar = GetComponent<FluentTAvatar>();
            }
        }

        private void OnEnable()
        {
            if (avatar != null)
            {
                // Subscribe to avatar callbacks
                avatar.callback.onSentenceStarted.AddListener(OnSentenceStarted);
                avatar.callback.onSentenceEnded.AddListener(OnSentenceEnded);
                avatar.callback.onTranscriptionReceived.AddListener(OnTranscriptionReceived);
                avatar.callback.onServerMotionTag.AddListener(OnServerMotionTag);

                // Build-time event for client-side emotion tagging
                avatar.callback.onSubtitleContentAdded.AddListener(OnSubtitleContentAdded);
            }
        }

        private void OnDisable()
        {
            if (avatar != null)
            {
                // Unsubscribe from avatar callbacks
                avatar.callback.onSentenceStarted.RemoveListener(OnSentenceStarted);
                avatar.callback.onSentenceEnded.RemoveListener(OnSentenceEnded);
                avatar.callback.onTranscriptionReceived.RemoveListener(OnTranscriptionReceived);
                avatar.callback.onServerMotionTag.RemoveListener(OnServerMotionTag);
                avatar.callback.onSubtitleContentAdded.RemoveListener(OnSubtitleContentAdded);

                // Unsubscribe from LateUpdate completion callback
                avatar.OnLateUpdateCompleted -= OnAvatarLateUpdateCompleted;
            }
        }

        private void Start()
        {
            // Initialize Animator and Override Controller
            if (avatar != null)
            {
                animator = avatar.GetComponent<Animator>();
                if (animator != null && animatorController != null)
                {
                    animator.runtimeAnimatorController = animatorController;
                    overrideController = new AnimatorOverrideController(animatorController);
                    animator.runtimeAnimatorController = overrideController;
                }
            }

            InitializeIdleAnimations();
            InitializeLookTarget();
            InitializeEmotionTagging();
            InitializeServerMotionTagging();
            InitializeEyeBlink();

            // Subscribe to avatar's LateUpdate completion callback
            // This ensures eye BlendShapes are updated AFTER face+head animation
            if (avatar != null)
            {
                avatar.OnLateUpdateCompleted += OnAvatarLateUpdateCompleted;
            }
        }

        /// <summary>
        /// Called by SwapBufferNotifier when a swap-buffer state becomes fully active.
        /// Swaps the OTHER slot's clip so it's ready before the next ExitTime transition.
        /// </summary>
        public void OnSwapSlotReady(SwapBufferNotifier.BufferGroup group, int activeSlot)
        {
            if (group == SwapBufferNotifier.BufferGroup.Idle)
                SwapInactiveIdleSlot(activeSlot);
        }

        private void LateUpdate()
        {
            if (enableLookTarget && lookTargetController != null)
            {
                UpdateLookTarget();
                // LateUpdateLookTarget(); // Moved to OnAvatarLateUpdateCompleted callback
            }
        }

        /// <summary>
        /// Called after FluentTAvatar's LateUpdate completes
        /// This ensures eye BlendShapes are updated AFTER face+head animation from Timeline
        /// </summary>
        private void OnAvatarLateUpdateCompleted()
        {
            if (enableLookTarget && lookTargetController != null)
            {
                LateUpdateLookTarget();
            }

            // Eye blink is handled by TMAnimationComponent on dedicated layer
        }

        private void OnDestroy()
        {
            // Unsubscribe from avatar callbacks
            if (avatar != null)
            {
                avatar.OnLateUpdateCompleted -= OnAvatarLateUpdateCompleted;
            }

            // Clean up virtual targets when avatar is destroyed
            if (enableLookTarget)
            {
                CleanupVirtualTargets();
            }
        }

        #region Callback Handlers

        public void OnSentenceStarted(FluentT.APIClient.V3.TalkMotionData data)
        {
            // Client-side emotion tagging is now handled at build time via onSubtitleContentAdded
            // Server motion tagging is handled by OnServerMotionTag callback (called at exact timing from timeline)
        }

        /// <summary>
        /// Called at build time when subtitle content is added to the timeline.
        /// Batch edit block is active, so markers added here are included in the same RebuildGraph.
        /// </summary>
        private void OnSubtitleContentAdded(FluentT.APIClient.V3.TalkMotionData data, string subtitleText, float startTime, float duration)
        {
            if (!enableTextEmotionDetection || string.IsNullOrEmpty(subtitleText) || avatar == null)
                return;

            ProcessEmotionTagging(subtitleText, duration, startTime, data);
        }

        public void OnSentenceEnded(FluentT.APIClient.V3.TalkMotionData data)
        {
            if (data != null && data.isLastSentence)
            {
                if (enableAutoEmotionReset)
                {
                    ResetEmotionState();
                }
            }
        }

        public void OnTranscriptionReceived(FluentT.Talkmotion.TranscriptionData data)
        {
            // Real-time transcription callback - called for each text chunk
            // This is especially useful for LiveKit mode where text arrives in small chunks

            if (data.isAgent)
            {
                // Agent is speaking - show real-time transcription
                // Example: Update UI subtitle in real-time
                // subtitleText.text = data.text;
            }
            else
            {
                // User is speaking - show what they said
                // Example: Update user input display
                // userInputText.text = data.text;
            }
        }

        #endregion

    }

    #region Data Structures for Idle Animation

    /// <summary>
    /// Idle animation entry with weight for weighted random selection.
    /// </summary>
    [System.Serializable]
    public class IdleAnimationEntry
    {
        public AnimationClip clip;
        [Range(0f, 10f)] public float weight = 1f;
        [Tooltip("Prevent this clip from playing twice in a row")]
        public bool preventRepeat;
    }

    #endregion

    #region Data Structures for Gesture Animation

    /// <summary>
    /// Emotion tag to gesture animation mapping.
    /// Multiple animation clips can be assigned per tag for random variant selection.
    /// </summary>
    [System.Serializable]
    public class GestureMapping
    {
        public string emotionTag;
        public List<AnimationClip> animationClips = new List<AnimationClip>();
        [Range(0f, 1f)]
        public float blendWeight = 1f;
    }

    #endregion
}
