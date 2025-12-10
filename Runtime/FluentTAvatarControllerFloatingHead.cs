using FluentT.Animation;
using FluentT.Talkmotion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        [Header("References")]
        [SerializeField] private FluentTAvatar avatar;

        [Header("Body Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] public List<AnimationClip> idleClips = new();
        [SerializeField] public List<AnimationClip> talkingClips = new();

        [Header("Body Animation Blend Settings")]
        [Tooltip("Time to blend in/out talking layer weight")]
        [SerializeField] [Range(0f, 2f)] private float talkingBlendTime = 0.5f;

        [Header("Look Target")]
        [SerializeField] private bool enableLookTarget = true;
        [SerializeField] private Transform lookTarget;

        [Header("Animation Rigging Multi-Aim Constraints")]
        [Tooltip("Multi-Aim Constraint for head tracking (required)")]
        [SerializeField] private MultiAimConstraint headAimConstraint;
        [Tooltip("Multi-Aim Constraint for left eye tracking (optional, only for Transform strategy)")]
        [SerializeField] private MultiAimConstraint leftEyeAimConstraint;
        [Tooltip("Multi-Aim Constraint for right eye tracking (optional, only for Transform strategy)")]
        [SerializeField] private MultiAimConstraint rightEyeAimConstraint;

        [Header("Look Target Transforms - For Auto-Find")]
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

        [Header("Client-Side Emotion Tagging")]
        [Tooltip("Enable client-side emotion tagging (uses TMAnimationClip for face)")]
        [SerializeField] public bool enableClientEmotionTagging = false;

        [Tooltip("Max emotion tags per sentence")]
        [SerializeField] public int maxEmotionTagsPerSentence = 1;

        [Tooltip("Word to emotion tag mappings")]
        [SerializeField] public List<WordEmotionMapping> wordEmotionMappings = new List<WordEmotionMapping>();

        [Tooltip("Emotion tag to motion mappings")]
        [SerializeField] public List<EmotionMotionMapping> emotionMotionMappings = new List<EmotionMotionMapping>();

        [Header("Server-Side Motion Tagging")]
        [Tooltip("Enable server-side motion tagging (uses AnimationClip for body)")]
        [SerializeField] public bool enableServerMotionTagging = false;

        [Tooltip("Emotion tag to animation clip mappings")]
        [SerializeField] public List<ServerMotionTagMapping> serverMotionTagMappings = new List<ServerMotionTagMapping>();

        [Header("Eye Blink")]
        [Tooltip("Enable automatic eye blink")]
        [SerializeField] public bool enableEyeBlink = false;

        [Tooltip("Average time between blinks in seconds")]
        [SerializeField] [Range(1f, 10f)] public float blinkInterval = 3f;

        [Tooltip("Random variance in blink timing (±seconds)")]
        [SerializeField] [Range(0f, 5f)] public float blinkIntervalVariance = 1f;

        // Private state
        private Animator animator;
        private AnimatorOverrideController overrideController;
        private bool isTalking = false;

        // Talking layer blend weight
        private float currentTalkingLayerWeight = 0f;
        private float targetTalkingLayerWeight = 0f;

        // Last played clips (to avoid repeating the same animation)
        [System.NonSerialized] public AnimationClip lastIdleClip;
        [System.NonSerialized] public AnimationClip lastTalkingClip;

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

                // Unsubscribe from LateUpdate completion callback
                avatar.OnLateUpdateCompleted -= OnAvatarLateUpdateCompleted;
            }
        }

        private void Start()
        {
            InitializeAnimator();
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

        private void Update()
        {
            UpdateTimelines(Time.deltaTime);
        }

        private void LateUpdate()
        {
            LateUpdateTimelines(Time.deltaTime);

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
            isTalking = true;

            // Start talking animations via state machine trigger
            StartTalkingAnimations(data?.audioClip);

            // Process client-side emotion tagging if enabled
            if (enableClientEmotionTagging && data != null && data.audioClip != null && !string.IsNullOrEmpty(data.text))
            {
                float startTime = Time.time;
                ProcessClientEmotionTagging(data.text, data.audioClip.length, startTime);
            }

            // NOTE: Server motion tagging is now handled by OnServerMotionTag callback (called at exact timing from timeline)
        }

        public void OnSentenceEnded(FluentT.APIClient.V3.TalkMotionData data)
        {
            isTalking = false;

            // Stop talking animations via state machine trigger
            StopTalkingAnimations(data?.audioClip);
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

        #region Animation Updates

        private void UpdateTimelines(float deltaTime)
        {
            // Update talking layer weight smoothly
            if (animator != null && talkingBlendTime > 0)
            {
                float blendSpeed = 1f / talkingBlendTime;
                currentTalkingLayerWeight = Mathf.MoveTowards(currentTalkingLayerWeight, targetTalkingLayerWeight, blendSpeed * deltaTime);
                animator.SetLayerWeight(1, currentTalkingLayerWeight); // Layer 1 is talking layer
            }

            // Update emotion tagging timeline
            UpdateEmotionTaggingTimeline(deltaTime);

            // Update server motion tagging timeline
            UpdateServerMotionTaggingTimeline(deltaTime);
        }

        private void LateUpdateTimelines(float deltaTime)
        {
            // LateUpdate emotion tagging timeline
            LateUpdateEmotionTaggingTimeline(deltaTime);

            // LateUpdate server motion tagging timeline
            LateUpdateServerMotionTaggingTimeline(deltaTime);
        }

        #endregion
    }

    #region Data Structures for Emotion Tagging

    /// <summary>
    /// Word to emotion tag mapping
    /// </summary>
    [System.Serializable]
    public class WordEmotionMapping
    {
        [Tooltip("Word or phrase to match (case-insensitive)")]
        public string word;

        [Tooltip("Emotion tag to apply")]
        public string emotionTag;

        [Tooltip("Priority (higher values take precedence)")]
        public int priority = 1;

        [Tooltip("Use partial matching (contains)")]
        public bool partialMatch = false;
    }

    /// <summary>
    /// Emotion tag to motion mapping
    /// </summary>
    [System.Serializable]
    public class EmotionMotionMapping
    {
        [Tooltip("Emotion tag")]
        public string emotionTag;

        [Tooltip("Animation clip to play")]
        public TMAnimationClip animationClip;

        [Tooltip("Blend weight (0-1)")]
        [Range(0f, 1f)]
        public float blendWeight = 1f;

        [Tooltip("Duration override (0 = use clip length)")]
        public float durationOverride = 0f;
    }

    /// <summary>
    /// Server motion tag to animation clip mapping
    /// </summary>
    [System.Serializable]
    public class ServerMotionTagMapping
    {
        [Tooltip("Emotion tag from server")]
        public string emotionTag;

        [Tooltip("Body animation clip to play (UnityEngine.AnimationClip)")]
        public AnimationClip animationClip;

        [Tooltip("Blend weight (0-1)")]
        [Range(0f, 1f)]
        public float blendWeight = 1f;

        [Tooltip("Duration override (0 = use clip length)")]
        public float durationOverride = 0f;
    }

    #endregion
}
