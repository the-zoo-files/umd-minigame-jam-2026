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

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            labels.Clear();

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

            foreach (CouchPlayerController player in CouchPlayerController.ActivePlayers)
            {
                OnPlayerJoined(player);
            }
        }

        private void OnDisable()
        {
            CouchPlayerController.PlayerJoined -= OnPlayerJoined;
            CouchPlayerController.PlayerLeft -= OnPlayerLeft;
        }

        private void OnPlayerJoined(CouchPlayerController player)
        {
            SetConnected(player.PlayerNumber, true);
        }

        private void OnPlayerLeft(CouchPlayerController player)
        {
            SetConnected(player.PlayerNumber, false);
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
