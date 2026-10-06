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
        private readonly Button[] cpuButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] difficultyButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] cpuCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] difficultyCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
        private readonly VisualElement[] colorSelectors = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] colorLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] previousColorButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] nextColorButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] previousColorCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] nextColorCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
        private readonly VisualElement[] characterSelectors = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] characterLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] previousCharacterButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] nextCharacterButtons = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] previousCharacterCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] nextCharacterCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];

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
                cpuButtons[slot] = root.Q<Button>($"addCpu{number}");
                difficultyButtons[slot] = root.Q<Button>($"cpuDifficulty{number}");
                colorSelectors[slot] = root.Q($"colorSelector{number}");
                colorLabels[slot] = root.Q<Label>($"playerColor{number}");
                previousColorButtons[slot] = root.Q<Button>($"previousColor{number}");
                nextColorButtons[slot] = root.Q<Button>($"nextColor{number}");
                characterSelectors[slot] = root.Q($"characterSelector{number}");
                characterLabels[slot] = root.Q<Label>($"playerCharacter{number}");
                previousCharacterButtons[slot] = root.Q<Button>($"previousCharacter{number}");
                nextCharacterButtons[slot] = root.Q<Button>($"nextCharacter{number}");
                if (cards[slot] == null || deviceLabels[slot] == null ||
                    stateLabels[slot] == null || leaveButtons[slot] == null ||
                    cpuButtons[slot] == null || difficultyButtons[slot] == null || colorSelectors[slot] == null ||
                    colorLabels[slot] == null || previousColorButtons[slot] == null || nextColorButtons[slot] == null ||
                    characterSelectors[slot] == null || characterLabels[slot] == null ||
                    previousCharacterButtons[slot] == null || nextCharacterButtons[slot] == null)
                {
                    Debug.LogError($"Connection menu is missing elements for Player {number}.", this);
                    enabled = false;
                    return;
                }

                int playerSlot = slot;
                leaveCallbacks[slot] = () => multiplayer.TryLeave(playerSlot);
                leaveButtons[slot].clicked += leaveCallbacks[slot];
                cpuCallbacks[slot] = () => multiplayer.TryAddCpu(playerSlot);
                difficultyCallbacks[slot] = () => CycleDifficulty(playerSlot);
                cpuButtons[slot].clicked += cpuCallbacks[slot];
                difficultyButtons[slot].clicked += difficultyCallbacks[slot];
                previousColorCallbacks[slot] = () => multiplayer.TryCyclePlayerColor(playerSlot, -1);
                nextColorCallbacks[slot] = () => multiplayer.TryCyclePlayerColor(playerSlot, 1);
                previousColorButtons[slot].clicked += previousColorCallbacks[slot];
                nextColorButtons[slot].clicked += nextColorCallbacks[slot];
                previousCharacterCallbacks[slot] = () => multiplayer.TryCyclePlayerCharacter(playerSlot, -1);
                nextCharacterCallbacks[slot] = () => multiplayer.TryCyclePlayerCharacter(playerSlot, 1);
                previousCharacterButtons[slot].clicked += previousCharacterCallbacks[slot];
                nextCharacterButtons[slot].clicked += nextCharacterCallbacks[slot];
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
                if (cpuButtons[slot] != null && cpuCallbacks[slot] != null) cpuButtons[slot].clicked -= cpuCallbacks[slot];
                if (difficultyButtons[slot] != null && difficultyCallbacks[slot] != null)
                    difficultyButtons[slot].clicked -= difficultyCallbacks[slot];
                if (previousColorButtons[slot] != null && previousColorCallbacks[slot] != null)
                    previousColorButtons[slot].clicked -= previousColorCallbacks[slot];
                if (nextColorButtons[slot] != null && nextColorCallbacks[slot] != null)
                    nextColorButtons[slot].clicked -= nextColorCallbacks[slot];
                if (previousCharacterButtons[slot] != null && previousCharacterCallbacks[slot] != null)
                    previousCharacterButtons[slot].clicked -= previousCharacterCallbacks[slot];
                if (nextCharacterButtons[slot] != null && nextCharacterCallbacks[slot] != null)
                    nextCharacterButtons[slot].clicked -= nextCharacterCallbacks[slot];
            }
        }

        private void PreviousCount()
        {
            multiplayer.TrySetPlayerCount(multiplayer.SelectedPlayerCount - 1);
        }

        private void CycleDifficulty(int slot)
        {
            CouchPlayerController player = multiplayer.GetParticipant(slot);
            if (player != null && player.IsCpu)
            {
                CpuDifficulty next = player.Cpu.Difficulty == CpuDifficulty.God ? CpuDifficulty.Noob : player.Cpu.Difficulty + 1;
                multiplayer.TrySetCpuDifficulty(slot, next);
            }
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
                CouchPlayerController participant = multiplayer.GetParticipant(slot);
                PlayerInput player = participant != null ? participant.HumanInput : null;
                bool isCpu = participant != null && participant.IsCpu;
                bool isConnected = CouchMultiplayerManager.IsReady(participant);
                bool isSelected = slot < selected;
                if (isConnected) connected++;
                cards[slot].EnableInClassList("is-selected", isSelected);
                cards[slot].EnableInClassList("is-connected", isConnected);
                cards[slot].EnableInClassList("is-disconnected", participant != null && !isConnected);
                cards[slot].EnableInClassList("is-cpu", isCpu);
                cards[slot].EnableInClassList("is-keyboard", player != null && player.currentControlScheme == "Keyboard&Mouse");
                deviceLabels[slot].text = isCpu ? "CPU" : player == null
                    ? (isSelected ? "Waiting for player" : "Not selected")
                    : (player.currentControlScheme == "Keyboard&Mouse" ? "Keyboard / Mouse" : "Controller");
                stateLabels[slot].text = isCpu ? "Ready" : isConnected ? "Connected"
                    : player != null ? "Reconnect device"
                    : isSelected ? "Not connected" : "Open slot";
                leaveButtons[slot].EnableInClassList("is-hidden", participant == null);
                leaveButtons[slot].text = isCpu ? "Remove" : "Leave";
                cpuButtons[slot].EnableInClassList("is-hidden", participant != null || !isSelected);
                difficultyButtons[slot].EnableInClassList("is-hidden", !isCpu);
                if (isCpu) difficultyButtons[slot].text = participant.Cpu.Difficulty.ToString();
                colorSelectors[slot].EnableInClassList("is-hidden", participant == null);
                characterSelectors[slot].EnableInClassList("is-hidden", participant == null);
                StyleColor border = participant != null ? new StyleColor(participant.PlayerColor) : new StyleColor(StyleKeyword.Null);
                cards[slot].style.borderTopColor = border;
                cards[slot].style.borderRightColor = border;
                cards[slot].style.borderBottomColor = border;
                cards[slot].style.borderLeftColor = border;
                if (participant != null)
                {
                    PlayerColorPalette.Entry color = PlayerColorPalette.Get(participant.ColorIndex);
                    colorLabels[slot].text = color.Name;
                    colorSelectors[slot].style.backgroundColor = color.Color;
                    colorLabels[slot].style.color = color.TextColor;
                    previousColorButtons[slot].style.color = color.TextColor;
                    nextColorButtons[slot].style.color = color.TextColor;
                    characterLabels[slot].text = participant.CharacterName;
                    bool hasMultipleCharacters = participant.CharacterCount > 1;
                    previousCharacterButtons[slot].SetEnabled(hasMultipleCharacters);
                    nextCharacterButtons[slot].SetEnabled(hasMultipleCharacters);
                }
            }

            statusLabel.text = multiplayer.StartFailure ??
                (multiplayer.CanStart ? "Ready to start" : $"{connected} / {selected} ready");
        }
    }
}
