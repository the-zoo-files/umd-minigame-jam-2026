using System.Collections.Generic;
using UmdJam.Gameplay;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class GameAudioFeedback : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;

        private readonly List<Button> buttons = new();
        private CouchMultiplayerManager lobby;
        private AudioSource clickSource;
        private AudioSource readySource;
        private AudioSource pointSource;
        private AudioSource pickupSource;
        private AudioSource connectionSource;
        private AudioSource launchSource;
        private AudioSource endSource;
        private AudioSource winnerSource;
        private readonly CouchPlayerController[] connectedPlayers = new CouchPlayerController[CouchMultiplayerManager.MaximumPlayers];
        private readonly bool[] connected = new bool[CouchMultiplayerManager.MaximumPlayers];
        private bool wasReady;

        private void Awake()
        {
            clickSource = CreateSource("Audio/ButtonClick");
            readySource = CreateSource("Audio/Ready");
            pointSource = CreateSource("Audio/Point");
            pickupSource = CreateSource("Audio/Pickup");
            connectionSource = CreateSource("Audio/Connection");
            launchSource = CreateSource("Audio/PotionLaunch");
            endSource = CreateSource("Audio/End");
            winnerSource = CreateSource("Audio/Winner");
        }

        private void OnEnable()
        {
            GetComponent<UIDocument>().rootVisualElement.Query<Button>().ToList(buttons);
            foreach (Button button in buttons) button.clicked += OnButtonClicked;
            lobby = FindAnyObjectByType<CouchMultiplayerManager>();
            wasReady = lobby != null && lobby.CanStart;
            RefreshConnections(false);
            if (lobby != null) lobby.LobbyChanged += OnLobbyChanged;
            PlayerFlaskCollector.FlaskCollected += OnFlaskCollected;
            PickupFlask.PickedUp += OnFlaskPickedUp;
            MachineFlaskShooter.FlaskLaunched += OnFlaskLaunched;
            GameManager.RoundEnded += OnRoundEnded;
            RoundResultsDirector.WinnersRevealed += OnWinnersRevealed;
        }

        private void OnDisable()
        {
            foreach (Button button in buttons) button.clicked -= OnButtonClicked;
            buttons.Clear();
            if (lobby != null) lobby.LobbyChanged -= OnLobbyChanged;
            PlayerFlaskCollector.FlaskCollected -= OnFlaskCollected;
            PickupFlask.PickedUp -= OnFlaskPickedUp;
            MachineFlaskShooter.FlaskLaunched -= OnFlaskLaunched;
            GameManager.RoundEnded -= OnRoundEnded;
            RoundResultsDirector.WinnersRevealed -= OnWinnersRevealed;
            if (clickSource != null) clickSource.Stop();
            if (readySource != null) readySource.Stop();
            if (pointSource != null) pointSource.Stop();
            if (pickupSource != null) pickupSource.Stop();
            if (connectionSource != null) connectionSource.Stop();
            if (launchSource != null) launchSource.Stop();
            if (endSource != null) endSource.Stop();
            if (winnerSource != null) winnerSource.Stop();
        }

        private void OnDestroy()
        {
            if (clickSource != null) Destroy(clickSource);
            if (readySource != null) Destroy(readySource);
            if (pointSource != null) Destroy(pointSource);
            if (pickupSource != null) Destroy(pickupSource);
            if (connectionSource != null) Destroy(connectionSource);
            if (launchSource != null) Destroy(launchSource);
            if (endSource != null) Destroy(endSource);
            if (winnerSource != null) Destroy(winnerSource);
        }

        private void OnButtonClicked() => Play(clickSource);

        private void OnRoundEnded() => Play(endSource);

        private void OnWinnersRevealed() => Play(winnerSource);

        private void OnFlaskPickedUp(PickupFlask flask)
        {
            if (flask != null && flask.IsHeld && GameManager.Instance != null && GameManager.Instance.IsPlaying)
                Play(pickupSource);
        }

        private void OnLobbyChanged()
        {
            RefreshConnections(true);
            bool ready = lobby != null && lobby.CanStart;
            if (ready && !wasReady && CouchPlayerController.ActivePlayers.Count > 0) Play(readySource);
            wasReady = ready;
        }

        private void RefreshConnections(bool announce)
        {
            for (int slot = 0; slot < connected.Length; slot++)
            {
                CouchPlayerController player = lobby != null ? lobby.GetParticipant(slot) : null;
                bool ready = player != null && !player.IsCpu && CouchMultiplayerManager.IsReady(player);
                if (announce && ready && (!connected[slot] || connectedPlayers[slot] != player)) Play(connectionSource);
                connected[slot] = ready;
                connectedPlayers[slot] = player;
            }
        }

        private void OnFlaskLaunched(MachineFlaskShooter machine)
        {
            if (machine != null && GameManager.Instance != null && GameManager.Instance.IsPlaying) Play(launchSource);
        }

        private void OnFlaskCollected(PlayerFlaskCollector collector, int points)
        {
            if (collector == null || points <= 0 || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
            var players = CouchPlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == null || players[i].PlayerNumber != collector.PlayerNumber) continue;
                Play(pointSource);
                return;
            }
        }

        private AudioSource CreateSource(string resourcePath)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"Missing game sound at Resources/{resourcePath}; this cue will be silent.", this);
                return null;
            }
            clip.LoadAudioData();
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            return source;
        }

        private void Play(AudioSource source)
        {
            if (source == null || soundVolume <= 0f) return;
            source.volume = soundVolume;
            // One voice per cue bounds overlap when buttons or collectors fire rapidly.
            source.Play();
        }
    }
}
