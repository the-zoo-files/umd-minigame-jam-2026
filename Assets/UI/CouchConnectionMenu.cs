using System;
using UmdJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UmdJam.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CouchConnectionMenu : MonoBehaviour
    {
        [SerializeField] private CouchMultiplayerManager multiplayer;

        private VisualElement menu;
        private VisualElement gameplayHud;
        private Label countLabel;
        private Label statusLabel;
        private Button previousButton;
        private Button nextButton;
        private Button startButton;
        private readonly VisualElement[] cards = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] deviceLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] stateLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] leaveButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] leaveCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            menu = root.Q("connectionMenu");
            gameplayHud = root.Q("gameplayHud");
            countLabel = root.Q<Label>("playerCount");
            statusLabel = root.Q<Label>("connectionStatus");
            previousButton = root.Q<Button>("previousPlayerCount");
            nextButton = root.Q<Button>("nextPlayerCount");
            startButton = root.Q<Button>("startGame");
            if (multiplayer == null || menu == null || gameplayHud == null || countLabel == null ||
                statusLabel == null || previousButton == null || nextButton == null || startButton == null)
            {
                Debug.LogError("Connection menu requires its multiplayer manager and named UI elements.", this);
                enabled = false;
                return;
            }

            for (int slot = 0; slot < cards.Length; slot++)
            {
                int number = slot + 1;
                cards[slot] = root.Q($"connectionPlayer{number}");
                deviceLabels[slot] = root.Q<Label>($"connectionDevice{number}");
                stateLabels[slot] = root.Q<Label>($"connectionState{number}");
                leaveButtons[slot] = root.Q<Button>($"leavePlayer{number}");
                if (cards[slot] == null || deviceLabels[slot] == null ||
                    stateLabels[slot] == null || leaveButtons[slot] == null)
                {
                    Debug.LogError($"Connection menu is missing elements for Player {number}.", this);
                    enabled = false;
                    return;
                }

                int playerSlot = slot;
                leaveCallbacks[slot] = () => multiplayer.TryLeave(playerSlot);
                leaveButtons[slot].clicked += leaveCallbacks[slot];
            }

            previousButton.clicked += PreviousCount;
            nextButton.clicked += NextCount;
            startButton.clicked += StartGame;
            multiplayer.LobbyChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (multiplayer != null)
            {
                multiplayer.LobbyChanged -= Refresh;
            }

            if (previousButton != null)
            {
                previousButton.clicked -= PreviousCount;
            }

            if (nextButton != null)
            {
                nextButton.clicked -= NextCount;
            }

            if (startButton != null)
            {
                startButton.clicked -= StartGame;
            }
            for (int slot = 0; slot < leaveButtons.Length; slot++)
            {
                if (leaveButtons[slot] != null && leaveCallbacks[slot] != null)
                {
                    leaveButtons[slot].clicked -= leaveCallbacks[slot];
                }
            }
        }

        private void PreviousCount()
        {
            multiplayer.TrySetPlayerCount(multiplayer.SelectedPlayerCount - 1);
        }

        private void NextCount()
        {
            multiplayer.TrySetPlayerCount(multiplayer.SelectedPlayerCount + 1);
        }

        private void StartGame()
        {
            multiplayer.TryStartGame();
        }

        private void Refresh()
        {
            bool isOpen = multiplayer.IsLobbyOpen;
            menu.EnableInClassList("is-hidden", !isOpen);
            gameplayHud.EnableInClassList("is-hidden", isOpen);
            if (!isOpen)
            {
                return;
            }

            int selected = multiplayer.SelectedPlayerCount;
            int connected = 0;
            countLabel.text = selected.ToString();
            previousButton.SetEnabled(multiplayer.CanSelectPlayerCount(selected - 1));
            nextButton.SetEnabled(multiplayer.CanSelectPlayerCount(selected + 1));
            startButton.SetEnabled(multiplayer.CanStart);
            for (int slot = 0; slot < cards.Length; slot++)
            {
                PlayerInput player = multiplayer.GetPlayer(slot);
                bool isConnected = CouchMultiplayerManager.IsConnected(player);
                bool isSelected = slot < selected;
                if (isConnected) connected++;
                cards[slot].EnableInClassList("is-selected", isSelected);
                cards[slot].EnableInClassList("is-connected", isConnected);
                cards[slot].EnableInClassList("is-disconnected", player != null && !isConnected);
                cards[slot].EnableInClassList("is-keyboard", player != null && player.currentControlScheme == "Keyboard&Mouse");
                deviceLabels[slot].text = player == null
                    ? (isSelected ? "Waiting for player" : "Not selected")
                    : (player.currentControlScheme == "Keyboard&Mouse" ? "Keyboard / Mouse" : "Controller");
                stateLabels[slot].text = isConnected ? "Connected"
                    : player != null ? "Reconnect device"
                    : isSelected ? "Not connected" : "Open slot";
                leaveButtons[slot].EnableInClassList("is-hidden", player == null);
            }

            statusLabel.text = multiplayer.CanStart ? "Ready to start" : $"{connected} / {selected} connected";
        }
    }
}
