using UnityEngine;

namespace UmdJam.Multiplayer
{
    public sealed class CharacterAnimationEvents : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.3f;
        [SerializeField, Min(0.02f)] private float minimumStepInterval = 0.12f;

        private CouchPlayerController player;
        private AudioSource footstepSource;
        private float nextStepAt;

        private void Awake()
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Footstep");
            if (clip == null)
            {
                Debug.LogWarning("Missing prepared footstep sound at Resources/Audio/Footstep.", this);
                return;
            }
            clip.LoadAudioData();
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.clip = clip;
            footstepSource.spatialBlend = 1f;
            footstepSource.rolloffMode = AudioRolloffMode.Linear;
            footstepSource.minDistance = 12f;
            footstepSource.maxDistance = 60f;
            footstepSource.dopplerLevel = 0f;
            footstepSource.priority = 160;
        }

        private void OnEnable() => GameManager.RoundEnded += StopFootsteps;

        private void OnDisable()
        {
            GameManager.RoundEnded -= StopFootsteps;
            StopFootsteps();
        }

        private void OnDestroy()
        {
            if (footstepSource != null) Destroy(footstepSource);
        }

        public void Configure(CouchPlayerController owner)
        {
            player = owner;
        }

        public void ReleaseFlask()
        {
            player?.ReleaseFlaskFromAnimation();
        }

        public void Footstep(int foot)
        {
            if (footstepSource == null || player == null || !player.IsWalking || !isActiveAndEnabled ||
                GameManager.Instance == null || !GameManager.Instance.IsPlaying || Time.timeScale <= 0f ||
                footstepVolume <= 0f || Time.time < nextStepAt) return;

            // Crossfading locomotion states can emit both clips' events; bound duplicate contacts.
            nextStepAt = Time.time + Mathf.Max(0.02f, minimumStepInterval);
            footstepSource.volume = footstepVolume;
            footstepSource.pitch = foot == 0 ? 0.98f : 1.02f;
            footstepSource.Play();
        }

        private void StopFootsteps()
        {
            if (footstepSource != null) footstepSource.Stop();
            nextStepAt = 0f;
        }
    }
}
