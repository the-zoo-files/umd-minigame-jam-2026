using UmdJam.Multiplayer;
using UnityEngine;

namespace UmdJam.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MachineAmbience : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float volume = 0.18f;
        [SerializeField] private Vector2 segmentSeconds = new(2.8f, 4.2f);
        [SerializeField] private Vector2 pitchRange = new(0.95f, 1.05f);
        [SerializeField, Min(0.01f)] private float crossfadeSeconds = 0.65f;

        private sealed class Voice
        {
            public AudioSource Source;
            public float Age;
            public float Duration;
            public float Gain;
            public bool Active;
        }

        private readonly Voice[] voices = new Voice[2];
        private System.Random random;
        private AudioClip clip;
        private int current;
        private bool running;
        private bool paused;
        private float previousOffset = -1f;

        private void Awake()
        {
            clip = Resources.Load<AudioClip>("Audio/Bubbling");
            if (clip == null || clip.length < 1f ||
                !float.IsFinite(volume) || !float.IsFinite(crossfadeSeconds) || crossfadeSeconds <= 0f ||
                !ValidRange(segmentSeconds, 0.2f, 10f) || !ValidRange(pitchRange, 0.5f, 2f))
            {
                Debug.LogWarning("Machine ambience requires the Bubbling clip and valid segment/pitch/fade settings.", this);
                enabled = false;
                return;
            }
            clip.LoadAudioData();
            // Ambience must not consume the gameplay RNG or change flask/hazard sequences.
            random = new System.Random();
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.clip = clip;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 12f;
                source.maxDistance = 60f;
                source.dopplerLevel = 0f;
                source.priority = 192;
                source.volume = 0f;
                voices[i] = new Voice { Source = source };
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsPlaying)
            {
                if (running) Stop();
                return;
            }
            bool shouldPause = Time.timeScale <= 0f;
            if (paused != shouldPause)
            {
                foreach (Voice voice in voices)
                {
                    if (!voice.Active) continue;
                    if (shouldPause) voice.Source.Pause();
                    else voice.Source.UnPause();
                }
                paused = shouldPause;
            }
            if (paused) return;
            Advance(Time.unscaledDeltaTime);
        }

        private void OnEnable() => GameManager.RoundEnded += Stop;

        private void Advance(float deltaTime)
        {
            if (!running)
            {
                running = true;
                StartVoice(0);
            }
            float fade = FadeDuration;
            foreach (Voice voice in voices)
            {
                if (!voice.Active) continue;
                voice.Age += deltaTime;
                float envelope = Mathf.Min(Mathf.Clamp01(voice.Age / fade),
                    Mathf.Clamp01((voice.Duration - voice.Age) / fade));
                voice.Source.volume = Mathf.Clamp01(volume) * voice.Gain * Mathf.Sin(envelope * Mathf.PI * 0.5f);
                if (voice.Age < voice.Duration) continue;
                voice.Source.Stop();
                voice.Active = false;
            }
            if (voices[current].Age >= voices[current].Duration - fade) StartVoice(1 - current);
        }

        private float FadeDuration => Mathf.Min(crossfadeSeconds,
            Mathf.Min(segmentSeconds.x * 0.45f, clip.length / pitchRange.y * 0.2f));

        private void StartVoice(int index)
        {
            Voice voice = voices[index];
            voice.Source.Stop();
            voice.Source.pitch = Range(pitchRange.x, pitchRange.y);
            voice.Duration = Mathf.Min(Range(segmentSeconds.x, segmentSeconds.y),
                (clip.length - 0.02f) / voice.Source.pitch);
            float available = Mathf.Max(0f, clip.length - voice.Duration * voice.Source.pitch - 0.02f);
            float offset = Range(0f, available);
            if (available > 0.6f && Mathf.Abs(offset - previousOffset) < 0.3f)
                offset = (offset + available * 0.5f) % available;
            previousOffset = offset;
            voice.Age = 0f;
            voice.Gain = Range(0.85f, 1.05f);
            voice.Active = true;
            voice.Source.volume = 0f;
            voice.Source.Play();
            voice.Source.timeSamples = Mathf.FloorToInt(offset * clip.frequency);
            current = index;
        }

        private float Range(float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);

        private static bool ValidRange(Vector2 value, float minimum, float maximum) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && value.x >= minimum &&
            value.y >= value.x && value.y <= maximum;

        private void Stop()
        {
            foreach (Voice voice in voices)
            {
                if (voice == null) continue;
                voice.Source.Stop();
                voice.Source.volume = 0f;
                voice.Active = false;
            }
            running = false;
            paused = false;
        }

        private void OnDisable()
        {
            GameManager.RoundEnded -= Stop;
            Stop();
        }

        private void OnDestroy()
        {
            foreach (Voice voice in voices)
                if (voice != null && voice.Source != null) Destroy(voice.Source);
        }
    }
}
