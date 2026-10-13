using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CouchPlayerHud : MonoBehaviour
    {
        private sealed class PlayerDisplay
        {
            public VisualElement Root;
            public VisualElement PortraitFrame;
            public Label Name;
            public Label Score;
        }

        private readonly Dictionary<int, PlayerDisplay> displays = new();
        private Label timerLabel;
        private Label eventLabel;
        private int displayedSeconds = -1;
        private bool resultsPresentationActive;

        public void SetResultsPresentationActive(bool active)
        {
            resultsPresentationActive = active;
            GetComponent<UIDocument>().rootVisualElement.EnableInClassList("results-active", active);
        }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (!TryGetComponent(out GameAudioFeedback audio)) gameObject.AddComponent<GameAudioFeedback>();
            if (!TryGetComponent(out GameplayFeedback feedback)) gameObject.AddComponent<GameplayFeedback>();
            root.EnableInClassList("results-active", resultsPresentationActive);
            displays.Clear();
            displayedSeconds = -1;
            timerLabel = root.Q<Label>("roundTimer");
            eventLabel = root.Q<Label>("randomEventStatus");
            RandomEventDirector.EventChanged += OnRandomEventChanged;
            OnRandomEventChanged(RandomEventDirector.Instance != null ? RandomEventDirector.Instance.ActiveEvent : RandomEventKind.None);
            if (timerLabel == null)
            {
                Debug.LogError("Missing round timer label in the HUD.", this);
            }

            for (int playerNumber = 1; playerNumber <= 4; playerNumber++)
            {
                Label label = root.Q<Label>($"player{playerNumber}Name");
                VisualElement card = root.Q($"player{playerNumber}Hud");
                VisualElement portrait = root.Q($"player{playerNumber}Portrait");
                VisualElement portraitFrame = card?.Q(className: "portrait-frame");
                Label score = root.Q<Label>($"player{playerNumber}Score");
                if (label == null || card == null || portrait == null || portraitFrame == null || score == null)
                {
                    Debug.LogError($"Missing HUD label for Player {playerNumber}.", this);
                    continue;
                }

                displays[playerNumber] = new PlayerDisplay { Root = card, PortraitFrame = portraitFrame, Name = label, Score = score };
                SetConnected(playerNumber, false);
            }

            CouchPlayerController.PlayerJoined += OnPlayerJoined;
            CouchPlayerController.PlayerLeft += OnPlayerLeft;
            CouchPlayerController.ScoreChanged += OnScoreChanged;
            CouchPlayerController.CharacterChanged += OnScoreChanged;
            CouchPlayerController.ColorChanged += OnScoreChanged;
            GameManager.RoundTimeChanged += OnRoundTimeChanged;

            foreach (CouchPlayerController player in CouchPlayerController.ActivePlayers)
            {
                OnPlayerJoined(player);
            }

            if (GameManager.Instance != null)
            {
                OnRoundTimeChanged(GameManager.Instance.RemainingTime);
            }
        }

        private void OnDisable()
        {
            CouchPlayerController.PlayerJoined -= OnPlayerJoined;
            CouchPlayerController.PlayerLeft -= OnPlayerLeft;
            CouchPlayerController.ScoreChanged -= OnScoreChanged;
            CouchPlayerController.CharacterChanged -= OnScoreChanged;
            CouchPlayerController.ColorChanged -= OnScoreChanged;
            GameManager.RoundTimeChanged -= OnRoundTimeChanged;
            RandomEventDirector.EventChanged -= OnRandomEventChanged;
        }

        private void OnRandomEventChanged(RandomEventKind kind)
        {
            if (eventLabel == null) return;
            eventLabel.text = RandomEventDirector.DisplayName(kind);
            eventLabel.EnableInClassList("is-hidden", kind == RandomEventKind.None);
        }

        private void OnPlayerJoined(CouchPlayerController player)
        {
            SetPlayer(player, true);
        }

        private void OnPlayerLeft(CouchPlayerController player)
        {
            SetConnected(player.PlayerNumber, false);
        }

        private void OnScoreChanged(CouchPlayerController player)
        {
            SetPlayer(player, true);
        }

        private void OnRoundTimeChanged(float remainingTime)
        {
            if (timerLabel == null)
            {
                return;
            }

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            if (totalSeconds == displayedSeconds)
            {
                return;
            }

            displayedSeconds = totalSeconds;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerLabel.text = $"{minutes:00}:{seconds:00}";
        }

        private void SetPlayer(CouchPlayerController player, bool isConnected)
        {
            if (!displays.TryGetValue(player.PlayerNumber, out PlayerDisplay display))
            {
                return;
            }

            display.Name.text = player.CharacterName;
            display.Score.text = player.Score.ToString();
            display.Score.style.fontSize = Mathf.Min(46f, 190f / display.Score.text.Length);
            display.Root.tooltip = player.DisplayName;
            display.PortraitFrame.style.borderTopColor = player.PlayerColor;
            display.PortraitFrame.style.borderRightColor = player.PlayerColor;
            display.PortraitFrame.style.borderBottomColor = player.PlayerColor;
            display.PortraitFrame.style.borderLeftColor = player.PlayerColor;
            display.Root.EnableInClassList("is-connected", isConnected);
        }

        private void SetConnected(int playerNumber, bool isConnected)
        {
            if (!displays.TryGetValue(playerNumber, out PlayerDisplay display))
            {
                return;
            }

            display.Name.text = $"Player {playerNumber}";
            display.Score.text = "0";
            display.Root.tooltip = string.Empty;
            display.Root.EnableInClassList("is-connected", isConnected);
        }
    }
}
