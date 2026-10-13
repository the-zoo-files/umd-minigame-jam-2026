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
        private Button randomEventsButton;
        private VisualElement eventOptions;
        private readonly Button[] eventButtons = new Button[3];
        private readonly Action[] eventCallbacks = new Action[3];
        private readonly VisualElement[] cards = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Button[] cardActions = new Button[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptGlyphs = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptGlyphShadowLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptGlyphStrokeLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly VisualElement[] promptGlyphStacks = new VisualElement[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptTexts = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptSizerLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Action[] cardActionCallbacks = new Action[CouchMultiplayerManager.MaximumPlayers];
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
        private readonly Label[] characterShadowLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] characterStrokeLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptShadowLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
        private readonly Label[] promptStrokeLabels = new Label[CouchMultiplayerManager.MaximumPlayers];
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
            randomEventsButton = root.Q<Button>("randomEvents");
            eventOptions = root.Q("eventOptions");
            if (multiplayer == null || menu == null || gameplayHud == null || countLabel == null ||
                statusLabel == null || previousButton == null || nextButton == null || startButton == null ||
                randomEventsButton == null || eventOptions == null)
            {
                Debug.LogError("Connection menu requires its multiplayer manager and named UI elements.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < eventButtons.Length; i++)
            {
                RandomEventKind kind = (RandomEventKind)(i + 1);
                eventButtons[i] = root.Q<Button>("event" + kind);
                if (eventButtons[i] == null)
                {
                    Debug.LogError("Connection menu is missing an event toggle.", this);
                    enabled = false;
                    return;
                }
                eventCallbacks[i] = () => multiplayer.TrySetRandomEvent(kind, !multiplayer.IsRandomEventSelected(kind));
                eventButtons[i].clicked += eventCallbacks[i];
            }

            for (int slot = 0; slot < cards.Length; slot++)
            {
                int number = slot + 1;
                cards[slot] = root.Q($"connectionPlayer{number}");
                cardActions[slot] = root.Q<Button>($"cardAction{number}");
                promptGlyphs[slot] = root.Q<Label>($"cardPromptGlyph{number}");
                promptGlyphShadowLabels[slot] = root.Q<Label>($"cardPromptGlyphShadow{number}");
                promptGlyphStrokeLabels[slot] = root.Q<Label>($"cardPromptGlyphStroke{number}");
                promptGlyphStacks[slot] = root.Q($"cardPromptGlyphStack{number}");
                promptTexts[slot] = root.Q<Label>($"cardPromptText{number}");
                promptSizerLabels[slot] = root.Q<Label>($"cardPromptSizer{number}");
                promptShadowLabels[slot] = root.Q<Label>($"cardPromptShadow{number}");
                promptStrokeLabels[slot] = root.Q<Label>($"cardPromptStroke{number}");
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
                characterShadowLabels[slot] = root.Q<Label>($"playerCharacterShadow{number}");
                characterStrokeLabels[slot] = root.Q<Label>($"playerCharacterStroke{number}");
                previousCharacterButtons[slot] = root.Q<Button>($"previousCharacter{number}");
                nextCharacterButtons[slot] = root.Q<Button>($"nextCharacter{number}");
                if (cards[slot] == null || cardActions[slot] == null || promptGlyphs[slot] == null ||
                    promptGlyphShadowLabels[slot] == null || promptGlyphStrokeLabels[slot] == null ||
                    promptGlyphStacks[slot] == null ||
                    promptTexts[slot] == null || promptSizerLabels[slot] == null ||
                    promptShadowLabels[slot] == null ||
                    promptStrokeLabels[slot] == null || deviceLabels[slot] == null ||
                    stateLabels[slot] == null || leaveButtons[slot] == null ||
                    cpuButtons[slot] == null || difficultyButtons[slot] == null || colorSelectors[slot] == null ||
                    colorLabels[slot] == null || previousColorButtons[slot] == null || nextColorButtons[slot] == null ||
                    characterSelectors[slot] == null || characterLabels[slot] == null ||
                    characterShadowLabels[slot] == null || characterStrokeLabels[slot] == null ||
                    previousCharacterButtons[slot] == null || nextCharacterButtons[slot] == null)
                {
                    Debug.LogError($"Connection menu is missing elements for Player {number}.", this);
                    enabled = false;
                    return;
                }

                int playerSlot = slot;
                cardActionCallbacks[slot] = () => ToggleReady(playerSlot);
                cardActions[slot].clicked += cardActionCallbacks[slot];
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
            randomEventsButton.clicked += ToggleRandomEvents;
            multiplayer.LobbyChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            for (int i = 0; i < eventButtons.Length; i++)
                if (eventButtons[i] != null && eventCallbacks[i] != null) eventButtons[i].clicked -= eventCallbacks[i];
            if (randomEventsButton != null) randomEventsButton.clicked -= ToggleRandomEvents;
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
                if (cardActions[slot] != null && cardActionCallbacks[slot] != null)
                    cardActions[slot].clicked -= cardActionCallbacks[slot];
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

        private void ToggleReady(int slot)
        {
            CouchPlayerController participant = multiplayer.GetParticipant(slot);
            if (participant == null) return;
            if (participant.IsCpu) multiplayer.TryLeave(slot);
            else multiplayer.TrySetPlayerReady(slot, !multiplayer.IsPlayerReady(participant));
        }

        private void Refresh()
        {
            randomEventsButton.text = multiplayer.RandomEventsEnabled ? "Random Events: On" : "Random Events: Off";
            randomEventsButton.EnableInClassList("is-on", multiplayer.RandomEventsEnabled);
            bool isOpen = multiplayer.IsLobbyOpen;
            randomEventsButton.SetEnabled(isOpen);
            eventOptions.SetEnabled(isOpen && multiplayer.RandomEventsEnabled);
            for (int i = 0; i < eventButtons.Length; i++)
            {
                RandomEventKind kind = (RandomEventKind)(i + 1);
                bool eventSelected = multiplayer.IsRandomEventSelected(kind);
                eventButtons[i].text = $"{RandomEventDirector.DisplayName(kind)}: {(eventSelected ? "On" : "Off")}";
                eventButtons[i].EnableInClassList("is-on", eventSelected);
            }
            eventOptions.tooltip = multiplayer.SelectedRandomEvents == 0 ?
                "No events selected: this round will have no hazards." : "Choose which events can appear this round.";
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
            startButton.SetEnabled(true);
            for (int slot = 0; slot < cards.Length; slot++)
            {
                CouchPlayerController participant = multiplayer.GetParticipant(slot);
                PlayerInput player = participant != null ? participant.HumanInput : null;
                bool isCpu = participant != null && participant.IsCpu;
                bool isConnected = CouchMultiplayerManager.IsReady(participant);
                bool isReady = multiplayer.IsPlayerReady(participant);
                bool isSelected = slot < selected;
                if (isReady) connected++;
                cards[slot].EnableInClassList("is-selected", isSelected);
                cards[slot].EnableInClassList("is-connected", isConnected);
                cards[slot].EnableInClassList("is-ready", isReady);
                cards[slot].EnableInClassList("is-empty", participant == null);
                cards[slot].EnableInClassList("is-unselected", !isSelected);
                cards[slot].EnableInClassList("is-disconnected", participant != null && !isConnected);
                cards[slot].EnableInClassList("is-cpu", isCpu);
                cards[slot].EnableInClassList("is-keyboard", player != null && player.currentControlScheme == "Keyboard&Mouse");
                UpdatePrompt(slot, participant, player, isReady);
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
                if (participant != null)
                {
                    PlayerColorPalette.Entry color = PlayerColorPalette.Get(participant.ColorIndex);
                    for (int i = 0; i < PlayerColorPalette.Count; i++)
                        cards[slot].RemoveFromClassList($"player-color-{i}");
                    cards[slot].AddToClassList($"player-color-{participant.ColorIndex}");
                    colorLabels[slot].text = color.Name;
                    characterLabels[slot].text = participant.CharacterName;
                    characterShadowLabels[slot].text = participant.CharacterName;
                    characterStrokeLabels[slot].text = participant.CharacterName;
                    bool hasMultipleCharacters = participant.CharacterCount > 1;
                    previousCharacterButtons[slot].SetEnabled(hasMultipleCharacters);
                    nextCharacterButtons[slot].SetEnabled(hasMultipleCharacters);
                }
            }

            statusLabel.text = multiplayer.StartFailure ??
                (multiplayer.CanStart ? "Ready to start" : $"{connected} / {selected} ready");
        }

        private void UpdatePrompt(int slot, CouchPlayerController participant, PlayerInput player, bool isReady)
        {
            Label glyph = promptGlyphs[slot];
            Label glyphShadow = promptGlyphShadowLabels[slot];
            Label glyphStroke = promptGlyphStrokeLabels[slot];
            VisualElement glyphStack = promptGlyphStacks[slot];
            glyphStack.RemoveFromClassList("input-keyboard");
            glyphStack.RemoveFromClassList("input-switch");
            glyphStack.RemoveFromClassList("input-xbox");

            if (participant == null)
            {
                glyph.text = string.Empty;
                glyphShadow.text = string.Empty;
                glyphStroke.text = string.Empty;
                promptTexts[slot].text = "Join";
                promptSizerLabels[slot].text = "Join";
                promptShadowLabels[slot].text = "Join";
                promptStrokeLabels[slot].text = "Join";
                cardActions[slot].SetEnabled(true);
                return;
            }

            cardActions[slot].SetEnabled(true);
            promptTexts[slot].text = isReady || participant.IsCpu ? "Cancel" : "Ready!";
            promptSizerLabels[slot].text = promptTexts[slot].text;
            promptShadowLabels[slot].text = promptTexts[slot].text;
            promptStrokeLabels[slot].text = promptTexts[slot].text;
            if (player != null && player.currentControlScheme == "Keyboard&Mouse")
            {
                glyphStack.AddToClassList("input-keyboard");
                glyph.text = isReady ? "\uE063" : "\uE05F";
                glyphShadow.text = glyph.text;
                glyphStroke.text = glyph.text;
                return;
            }

            if (IsNintendoController(player))
            {
                glyphStack.AddToClassList("input-switch");
                glyph.text = isReady ? "\uE005" : "\uE007";
                glyphShadow.text = glyph.text;
                glyphStroke.text = glyph.text;
                return;
            }

            glyphStack.AddToClassList("input-xbox");
            glyph.text = isReady || participant.IsCpu ? "\uE007" : "\uE005";
            glyphShadow.text = glyph.text;
            glyphStroke.text = glyph.text;
        }

        private static bool IsNintendoController(PlayerInput player)
        {
            if (player == null) return false;
            foreach (InputDevice device in player.devices)
            {
                string identity = $"{device.layout} {device.description.manufacturer} {device.description.product}";
                if (identity.IndexOf("Nintendo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    identity.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private void ToggleRandomEvents() => multiplayer.TrySetRandomEvents(!multiplayer.RandomEventsEnabled);
    }
}
