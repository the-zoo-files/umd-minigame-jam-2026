using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    // Presentation only. Penalty ownership and collection rewards remain in gameplay.
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameplayFeedback : MonoBehaviour
    {
        [SerializeField, Range(3, 30)] private int countdownSeconds = 10;
        [SerializeField, Range(0.2f, 3f)] private float scoringDuration = 0.85f;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.3f;

        private VisualElement root;
        private Label timer;
        private Camera arenaCamera;
        private readonly Label[] riskLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly CouchPlayerController[] participants = new CouchPlayerController[CouchMultiplayerManager.MaximumPlayers];
        private readonly int[] penalties = new int[CouchMultiplayerManager.MaximumPlayers];
        private readonly int[] displayedPenalties = new int[CouchMultiplayerManager.MaximumPlayers];
        private readonly Color[] riskColors = new Color[CouchMultiplayerManager.MaximumPlayers];
        private readonly CollectionPresentation[] collections = new CollectionPresentation[CouchMultiplayerManager.MaximumPlayers];
        private int displayedSeconds = -1;
        private bool urgent;
        private bool timerPulsing;
        private float timerPulseStarted;
        private AudioSource audioSource;
        private AudioClip countdownSound;

        private sealed class CollectionPresentation
        {
            public Label Popup;
            public VisualElement Pulse;
            public Vector3 Position;
            public long Points;
            public float Started;
            public bool Active;
        }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement.Q("feedbackOverlay");
            VisualElement document = GetComponent<UIDocument>().rootVisualElement;
            timer = document.Q<Label>("roundTimer");
            VisualElement collectionRoot = root?.Q("collectionFeedback");
            arenaCamera = Camera.main;
            for (int i = 0; i < riskLabels.Length; i++) riskLabels[i] = root?.Q<Label>($"zoneRisk{i + 1}");
            if (root == null || timer == null || collectionRoot == null ||
                arenaCamera == null || System.Array.Exists(riskLabels, label => label == null))
            {
                Debug.LogError("Gameplay feedback requires its camera and named HUD elements.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < collections.Length; i++)
            {
                Label popup = new() { name = $"scorePopup{i + 1}", pickingMode = PickingMode.Ignore };
                popup.AddToClassList("score-popup");
                popup.AddToClassList("is-hidden");
                VisualElement pulse = new() { name = $"collectorPulse{i + 1}", pickingMode = PickingMode.Ignore };
                pulse.AddToClassList("collector-pulse");
                pulse.AddToClassList("is-hidden");
                collectionRoot.Add(pulse);
                collectionRoot.Add(popup);
                collections[i] = new CollectionPresentation { Popup = popup, Pulse = pulse };
                displayedPenalties[i] = -1;
            }
            CreateAudio();
            GameManager.RoundTimeChanged += OnRoundTimeChanged;
            GameManager.RoundEnded += Clear;
            PlayerFlaskCollector.FlaskCollected += OnFlaskCollected;
            displayedSeconds = -1;
            if (GameManager.Instance != null) OnRoundTimeChanged(GameManager.Instance.RemainingTime);
        }

        private void OnDisable()
        {
            GameManager.RoundTimeChanged -= OnRoundTimeChanged;
            GameManager.RoundEnded -= Clear;
            PlayerFlaskCollector.FlaskCollected -= OnFlaskCollected;
            Clear();
            foreach (CollectionPresentation collection in collections)
            {
                collection?.Popup.RemoveFromHierarchy();
                collection?.Pulse.RemoveFromHierarchy();
            }
        }

        private void OnDestroy()
        {
            if (countdownSound != null) Destroy(countdownSound);
            if (audioSource != null) Destroy(audioSource);
        }

        private void OnRoundTimeChanged(float remainingTime)
        {
            GameManager game = GameManager.Instance;
            bool wasUrgent = urgent;
            urgent = game != null && game.IsPlaying && remainingTime > 0f && remainingTime <= countdownSeconds;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            if (seconds == displayedSeconds && wasUrgent == urgent) return;
            if (timer != null)
            {
                timer.EnableInClassList("is-urgent", urgent);
                timer.EnableInClassList("is-critical", urgent && remainingTime <= 3f);
            }
            if (!urgent)
            {
                if (wasUrgent) HideRisks();
                ResetTimerPulse();
            }
            bool hadPreviousSample = displayedSeconds >= 0;
            displayedSeconds = seconds;
            if (!urgent) return;
            timerPulseStarted = Time.time;
            timerPulsing = true;
            if (hadPreviousSample && audioSource != null && soundVolume > 0f)
                audioSource.PlayOneShot(countdownSound, soundVolume * Mathf.Lerp(0.4f, 1f, 1f - remainingTime / countdownSeconds));
        }

        private void OnFlaskCollected(PlayerFlaskCollector collector, int points)
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying || points <= 0 || collector == null) return;
            int slot = collector.PlayerNumber - 1;
            if (slot < 0 || slot >= collections.Length) return;
            CouchPlayerController participant = FindParticipant(slot + 1);
            if (participant == null) return;
            CollectionPresentation collection = collections[slot];
            bool accumulating = collection.Active && Time.time - collection.Started < scoringDuration;
            collection.Points = accumulating ? collection.Points + points : points;
            collection.Started = Time.time;
            collection.Position = collector.CollectionPoint;
            collection.Active = true;
            collection.Popup.text = $"+{collection.Points}";
            SetBorder(collection.Popup, participant.PlayerColor);
            SetBorder(collection.Pulse, participant.PlayerColor);
            collection.Popup.RemoveFromClassList("is-hidden");
            collection.Pulse.RemoveFromClassList("is-hidden");
        }

        private void LateUpdate()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying || root == null || root.panel == null || arenaCamera == null) return;
            if (timerPulsing)
            {
                float progress = Mathf.Clamp01((Time.time - timerPulseStarted) / 0.2f);
                timer.style.scale = new Scale(Vector3.one * (1f + 0.15f * (1f - progress)));
                if (progress >= 1f) ResetTimerPulse();
            }
            for (int i = 0; i < collections.Length; i++)
            {
                CollectionPresentation collection = collections[i];
                if (!collection.Active) continue;
                float progress = Mathf.Clamp01((Time.time - collection.Started) / Mathf.Max(0.2f, scoringDuration));
                if (progress >= 1f)
                {
                    HideCollection(collection);
                    continue;
                }
                Position(collection.Popup, collection.Position + Vector3.up, -24f * progress);
                Position(collection.Pulse, collection.Position, 0f);
                collection.Popup.style.opacity = 1f - progress * progress;
                collection.Pulse.style.opacity = 1f - progress;
                collection.Pulse.style.scale = new Scale(Vector3.one * (1f + progress));
            }
            if (urgent) UpdateRisks(game);
        }

        private void UpdateRisks(GameManager game)
        {
            System.Array.Clear(penalties, 0, penalties.Length);
            System.Array.Clear(participants, 0, participants.Length);
            var players = CouchPlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                CouchPlayerController player = players[i];
                if (player != null && player.PlayerNumber >= 1 && player.PlayerNumber <= participants.Length)
                    participants[player.PlayerNumber - 1] = player;
            }
            var flasks = PickupFlask.ActiveFlasks;
            for (int i = 0; i < flasks.Count; i++)
            {
                PickupFlask flask = flasks[i];
                int number = game.GetFlaskPenaltyPlayer(flask);
                if (number == 0 || participants[number - 1] == null || flask.PointValue <= 0) continue;
                int points = flask.PointValue;
                penalties[number - 1] += points;
            }
            for (int i = 0; i < riskLabels.Length; i++)
            {
                bool occupied = participants[i] != null;
                riskLabels[i].EnableInClassList("is-hidden", !occupied);
                if (!occupied) continue;
                if (displayedPenalties[i] != penalties[i])
                {
                    riskLabels[i].text = penalties[i] == 0 ? "At risk: 0" : $"At risk: −{penalties[i]}";
                    displayedPenalties[i] = penalties[i];
                }
                Color color = participants[i].PlayerColor;
                if (!riskColors[i].Equals(color))
                {
                    riskLabels[i].style.borderLeftColor = color;
                    riskColors[i] = color;
                }
            }
        }

        private void Position(VisualElement element, Vector3 world, float verticalOffset)
        {
            bool visible = arenaCamera.WorldToViewportPoint(world).z > 0f;
            element.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            if (!visible) return;
            Vector2 point = root.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, arenaCamera));
            element.style.left = point.x;
            element.style.top = point.y + verticalOffset;
        }

        private static CouchPlayerController FindParticipant(int number)
        {
            var players = CouchPlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && players[i].PlayerNumber == number) return players[i];
            return null;
        }

        private static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        private void ResetTimerPulse()
        {
            if (timerPulsing && timer != null) timer.style.scale = new Scale(Vector3.one);
            timerPulsing = false;
        }

        private void HideRisks()
        {
            foreach (Label label in riskLabels) label?.AddToClassList("is-hidden");
        }

        private static void HideCollection(CollectionPresentation collection)
        {
            collection.Active = false;
            collection.Popup.AddToClassList("is-hidden");
            collection.Pulse.AddToClassList("is-hidden");
        }

        private void Clear()
        {
            urgent = false;
            timer?.RemoveFromClassList("is-urgent");
            timer?.RemoveFromClassList("is-critical");
            ResetTimerPulse();
            HideRisks();
            foreach (CollectionPresentation collection in collections)
                if (collection != null) HideCollection(collection);
            if (audioSource != null) audioSource.Stop();
        }

        private void CreateAudio()
        {
            if (audioSource != null) return;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            countdownSound = CreateSound("Countdown tick", new[] { 880f }, 0.045f);
        }

        private static AudioClip CreateSound(string name, float[] notes, float duration)
        {
            const int sampleRate = 16000;
            float[] samples = new float[Mathf.CeilToInt(sampleRate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float noteProgress = (float)i / samples.Length * notes.Length;
                int note = Mathf.Min((int)noteProgress, notes.Length - 1);
                float phase = noteProgress - note;
                float time = phase * duration / notes.Length;
                samples[i] = Mathf.Sin(time * notes[note] * Mathf.PI * 2f) * Mathf.Sin(phase * Mathf.PI) * 0.3f;
            }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
