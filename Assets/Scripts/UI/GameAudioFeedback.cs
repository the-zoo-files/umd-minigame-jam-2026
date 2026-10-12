using System.Collections.Generic;
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
        private bool wasReady;

        private void Awake()
        {
            clickSource = CreateSource("Audio/ButtonClick");
            readySource = CreateSource("Audio/Ready");
            pointSource = CreateSource("Audio/Point");
        }

        private void OnEnable()
        {
            GetComponent<UIDocument>().rootVisualElement.Query<Button>().ToList(buttons);
            foreach (Button button in buttons) button.clicked += OnButtonClicked;
            lobby = FindAnyObjectByType<CouchMultiplayerManager>();
            wasReady = lobby != null && lobby.CanStart;
            if (lobby != null) lobby.LobbyChanged += OnLobbyChanged;
            PlayerFlaskCollector.FlaskCollected += OnFlaskCollected;
        }

        private void OnDisable()
        {
            foreach (Button button in buttons) button.clicked -= OnButtonClicked;
            buttons.Clear();
            if (lobby != null) lobby.LobbyChanged -= OnLobbyChanged;
            PlayerFlaskCollector.FlaskCollected -= OnFlaskCollected;
            if (clickSource != null) clickSource.Stop();
            if (readySource != null) readySource.Stop();
            if (pointSource != null) pointSource.Stop();
        }

        private void OnDestroy()
        {
            if (clickSource != null) Destroy(clickSource);
            if (readySource != null) Destroy(readySource);
            if (pointSource != null) Destroy(pointSource);
        }

        private void OnButtonClicked() => Play(clickSource);

        private void OnLobbyChanged()
        {
            bool ready = lobby != null && lobby.CanStart;
            if (ready && !wasReady) Play(readySource);
            wasReady = ready;
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
