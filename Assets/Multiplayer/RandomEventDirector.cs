using System;
using UmdJam.Gameplay;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    // Scene-owned scheduling and world forces. All participants, including God CPUs, use the same effects.
    [DisallowMultipleComponent]
    public sealed class RandomEventDirector : MonoBehaviour
    {
        public static RandomEventDirector Instance { get; private set; }
        public static event Action<RandomEventKind> EventChanged;

        [SerializeField, Min(1f)] private float minimumQuietDuration = 12f;
        [SerializeField, Min(1f)] private float maximumQuietDuration = 20f;
        [SerializeField, Min(1f)] private float eventDuration = 6f;
        [SerializeField, Min(0f)] private float earthquakeDriftSpeed = 4f;
        [SerializeField, Min(0f)] private float earthquakeFlaskAcceleration = 8f;
        [SerializeField, Min(0.5f)] private float tornadoRadius = 2.2f;
        [SerializeField, Min(1f)] private float tornadoHeight = 4.5f;
        [SerializeField, Min(0f)] private float tornadoSpeed = 7f;
        [SerializeField, Min(0.2f)] private float lightningWarning = 0.9f;
        [SerializeField, Min(0.1f)] private float lightningStun = 0.9f;
        [SerializeField, Min(0.5f)] private float lightningRadius = 1.8f;

        private GameManager round;
        private RandomEventVisuals visuals;
        private System.Random random;
        private Bounds arena;
        private float nextTransition;
        private float nextStrike;
        private float eventStarted;
        private float lastStrike = float.NegativeInfinity;
        private int eventBag;
        private RandomEventKind previousEvent;
        private readonly Vector3[] tornadoOrigins = new Vector3[2];

        public RandomEventKind ActiveEvent { get; private set; }
        public Vector3 LightningPosition { get; private set; }
        public float LightningRadius => lightningRadius;
        public float TornadoHeight => tornadoHeight;
        public float TornadoRadius => tornadoRadius;
        public float StrikeFlash => Mathf.Clamp01(1f - (Time.time - lastStrike) / 0.25f);
        public bool IsWarning => ActiveEvent == RandomEventKind.Lightning && Time.time < nextStrike;
        public float Elapsed => Time.time - eventStarted;

        public static string DisplayName(RandomEventKind kind) => kind switch
        {
            RandomEventKind.Earthquake => "Earthquake",
            RandomEventKind.Tornadoes => "Tornadoes",
            RandomEventKind.Lightning => "Lightning",
            _ => string.Empty
        };

        public bool Configure(GameManager game)
        {
            if (round != null) return round == game && isActiveAndEnabled && visuals != null;
            if (!isActiveAndEnabled || game == null || !Valid(minimumQuietDuration, 0.1f, 120f) ||
                !Valid(maximumQuietDuration, minimumQuietDuration, 120f) ||
                !Valid(eventDuration, 0.1f, 30f) || !Valid(earthquakeDriftSpeed, 0f, 10f) ||
                !Valid(earthquakeFlaskAcceleration, 0f, 40f) || !Valid(tornadoRadius, 0.5f, 4f) ||
                !Valid(tornadoHeight, 1f, 8f) || !Valid(tornadoSpeed, 0f, 15f) ||
                !Valid(lightningWarning, 0.2f, 5f) || !Valid(lightningStun, 0.1f, 5f) ||
                !Valid(lightningRadius, 0.5f, 4f) || Resources.Load<Material>("RandomEvents/Effects") == null)
            {
                Debug.LogError("Random events require valid bounded tuning and the Effects material.", this);
                enabled = false;
                return false;
            }
            round = game;
            arena = game.ArenaBounds;
            random = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            visuals = gameObject.AddComponent<RandomEventVisuals>();
            visuals.Configure(this);
            return true;
        }

        private static bool Valid(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            EventChanged = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameManager.RoundStarted += OnRoundStarted;
        }

        private void OnDisable()
        {
            GameManager.RoundStarted -= OnRoundStarted;
            StopEffects();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (visuals != null) Destroy(visuals);
        }

        private void OnRoundStarted() => ScheduleQuietInterval();

        private void ScheduleQuietInterval()
        {
            // Teardown can run before configuration initializes the random source.
            float duration = minimumQuietDuration;
            if (random != null)
                duration += (float)random.NextDouble() * (maximumQuietDuration - minimumQuietDuration);
            nextTransition = Time.time + duration;
        }

        private void Update()
        {
            if (round == null || !round.IsPlaying || random == null) return;
            if (Time.time >= nextTransition)
            {
                if (ActiveEvent == RandomEventKind.None) TryBeginEvent(ChooseEvent());
                else FinishEvent();
            }
            if (ActiveEvent == RandomEventKind.Lightning && Time.time >= nextStrike)
            {
                lastStrike = Time.time;
                var players = CouchPlayerController.ActivePlayers;
                for (int i = 0; i < players.Count; i++)
                {
                    CouchPlayerController player = players[i];
                    if (player != null && player.isActiveAndEnabled &&
                        CpuNavigation.PlanarDistance(player.transform.position, LightningPosition) <= lightningRadius)
                        player.TryStun(lightningStun);
                }
                nextStrike = Time.time + lightningWarning + 0.7f;
                // Keep the bolt at its strike position for the flash before choosing the next warning.
            }
            if (ActiveEvent == RandomEventKind.Lightning && Time.time - lastStrike >= 0.3f &&
                nextStrike - Time.time > lightningWarning)
            {
                LightningPosition = ChooseStrikePosition();
                nextStrike = Time.time + lightningWarning;
            }
        }

        public bool TryBeginEvent(RandomEventKind kind)
        {
            if (!isActiveAndEnabled || round == null || !round.IsPlaying || random == null ||
                ActiveEvent != RandomEventKind.None || kind < RandomEventKind.Earthquake || kind > RandomEventKind.Lightning)
                return false;
            ActiveEvent = kind;
            previousEvent = kind;
            eventStarted = Time.time;
            nextTransition = Time.time + eventDuration;
            lastStrike = float.NegativeInfinity;
            for (int i = 0; i < tornadoOrigins.Length; i++) tornadoOrigins[i] = RandomArenaPosition(3f);
            LightningPosition = ChooseStrikePosition();
            nextStrike = Time.time + lightningWarning;
            EventChanged?.Invoke(kind);
            return true;
        }

        public void StopEffects()
        {
            FinishEvent();
            var players = CouchPlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null) players[i].ClearEnvironmentalEffects();
        }

        private void FinishEvent()
        {
            ActiveEvent = RandomEventKind.None;
            ScheduleQuietInterval();
            lastStrike = float.NegativeInfinity;
            if (visuals != null) visuals.ResetPresentation();
            EventChanged?.Invoke(RandomEventKind.None);
        }

        public Vector3 GetEarthquakeDrift(int playerNumber)
        {
            if (ActiveEvent != RandomEventKind.Earthquake || round == null || !round.IsPlaying) return Vector3.zero;
            float phase = Time.time * 3.5f + (playerNumber & 1023) * 7.13f;
            return new Vector3(Mathf.PerlinNoise(phase, 1f) * 2f - 1f, 0f,
                Mathf.PerlinNoise(2f, phase) * 2f - 1f) * earthquakeDriftSpeed;
        }

        public Vector3 TornadoPosition(int index)
        {
            Vector3 position = tornadoOrigins[index];
            float phase = Elapsed * 0.6f + index * Mathf.PI;
            position += new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase)) * 1.2f;
            position.x = Mathf.Clamp(position.x, arena.min.x + 2f, arena.max.x - 2f);
            position.z = Mathf.Clamp(position.z, arena.min.z + 2f, arena.max.z - 2f);
            return position;
        }

        public bool TryGetTornadoVelocity(Vector3 position, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (ActiveEvent != RandomEventKind.Tornadoes || round == null || !round.IsPlaying) return false;
            for (int i = 0; i < tornadoOrigins.Length; i++)
            {
                Vector3 radial = position - TornadoPosition(i);
                radial.y = 0f;
                if (radial.sqrMagnitude > tornadoRadius * tornadoRadius ||
                    position.y > arena.min.y + tornadoHeight + 1f) continue;
                if (radial.sqrMagnitude < 0.01f) radial = Vector3.right * 0.1f;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial.normalized);
                float lift = Mathf.Clamp01((arena.min.y + tornadoHeight - position.y) / tornadoHeight);
                velocity = tangent * tornadoSpeed - radial * 2f + Vector3.up * (8f * lift);
                return true;
            }
            return false;
        }

        private void FixedUpdate()
        {
            if (round == null || !round.IsPlaying ||
                (ActiveEvent != RandomEventKind.Earthquake && ActiveEvent != RandomEventKind.Tornadoes)) return;
            var flasks = PickupFlask.ActiveFlasks;
            for (int i = 0; i < flasks.Count; i++)
            {
                PickupFlask flask = flasks[i];
                if (flask == null || !flask.IsAvailable) continue;
                if (ActiveEvent == RandomEventKind.Earthquake)
                {
                    Vector3 drift = GetEarthquakeDrift(flask.GetEntityId().GetHashCode()) * earthquakeFlaskAcceleration;
                    flask.ApplyEnvironmentalAcceleration(Vector3.ClampMagnitude(drift, earthquakeFlaskAcceleration) +
                        Vector3.up * 2f, Vector3.zero);
                }
                else if (TryGetTornadoVelocity(flask.transform.position, out Vector3 wind))
                {
                    flask.ApplyEnvironmentalAcceleration(Vector3.ClampMagnitude(wind - flask.Velocity, 20f) * 4f,
                        Vector3.up * 3f);
                }
            }
        }

        private RandomEventKind ChooseEvent()
        {
            if (eventBag == 0) eventBag = 0b1110;
            int first = random.Next(1, 4);
            for (int offset = 0; offset < 3; offset++)
            {
                RandomEventKind kind = (RandomEventKind)(1 + (first - 1 + offset) % 3);
                if ((eventBag & (1 << (int)kind)) == 0 ||
                    (kind == previousEvent && (eventBag & ~(1 << (int)kind)) != 0)) continue;
                eventBag &= ~(1 << (int)kind);
                return kind;
            }
            eventBag &= ~(1 << (int)previousEvent);
            return previousEvent;
        }

        private Vector3 ChooseStrikePosition()
        {
            var players = CouchPlayerController.ActivePlayers;
            if (players.Count > 0)
            {
                CouchPlayerController player = players[random.Next(players.Count)];
                if (player != null && player.isActiveAndEnabled)
                {
                    Vector3 point = player.transform.position;
                    point.y = arena.min.y + 0.08f;
                    return point;
                }
            }
            return RandomArenaPosition(1f);
        }

        private Vector3 RandomArenaPosition(float margin)
        {
            Vector3 extent = arena.extents;
            float x = Mathf.Max(0f, extent.x - margin);
            float z = Mathf.Max(0f, extent.z - margin);
            return new Vector3(arena.center.x + ((float)random.NextDouble() * 2f - 1f) * x,
                arena.min.y + 0.08f, arena.center.z + ((float)random.NextDouble() * 2f - 1f) * z);
        }
    }
}
