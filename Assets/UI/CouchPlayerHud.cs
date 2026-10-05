using System.Collections.Generic;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CouchPlayerHud : MonoBehaviour
    {
        private readonly Dictionary<int, Label> labels = new();
        private Label timerLabel;
        private int displayedSeconds = -1;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            labels.Clear();
            displayedSeconds = -1;
            timerLabel = root.Q<Label>("roundTimer");
            if (timerLabel == null)
            {
                Debug.LogError("Missing round timer label in the HUD.", this);
            }

            for (int playerNumber = 1; playerNumber <= 4; playerNumber++)
            {
                Label label = root.Q<Label>($"player{playerNumber}Name");
                if (label == null)
                {
                    Debug.LogError($"Missing HUD label for Player {playerNumber}.", this);
                    continue;
                }

                labels[playerNumber] = label;
                SetConnected(playerNumber, false);
            }

            CouchPlayerController.PlayerJoined += OnPlayerJoined;
            CouchPlayerController.PlayerLeft += OnPlayerLeft;
            CouchPlayerController.ScoreChanged += OnScoreChanged;
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
            GameManager.RoundTimeChanged -= OnRoundTimeChanged;
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
            if (!labels.TryGetValue(player.PlayerNumber, out Label label))
            {
                return;
            }

            label.text = $"Player {player.PlayerNumber}\nScore: {player.Score}";
            label.EnableInClassList("is-connected", isConnected);
        }

        private void SetConnected(int playerNumber, bool isConnected)
        {
            if (!labels.TryGetValue(playerNumber, out Label label))
            {
                return;
            }

            label.text = $"Player {playerNumber}";
            label.EnableInClassList("is-connected", isConnected);
        }
    }
}
