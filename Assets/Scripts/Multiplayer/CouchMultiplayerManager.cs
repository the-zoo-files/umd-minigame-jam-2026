using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UmdJam.Multiplayer
{
    [RequireComponent(typeof(PlayerInputManager))]
    public sealed class CouchMultiplayerManager : MonoBehaviour
    {
        public const int MaximumPlayers = 4;

        public event Action LobbyChanged;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private int[] defaultPlayerColors = { 0, 1, 5, 2 };

        private int selectedPlayerCount = MaximumPlayers;
        private PlayerInputManager manager;
        private InputActionMap lobbyActions;
        private bool refreshPending;
        private readonly HashSet<CouchPlayerController> leavingPlayers = new();
        private readonly HashSet<CouchPlayerController> readyPlayers = new();
        private CpuNavigation navigation;
        private float retryStartAt;
        private bool setupSubmitReleased;
        private float setupStartAllowedAt;

        public string StartFailure { get; private set; }

        public int SelectedPlayerCount => selectedPlayerCount;
        public bool IsSetupOpen { get; private set; }
        public CpuDifficulty SelectedCpuDifficulty { get; private set; } = CpuDifficulty.Pro;
        // Selection only: the future match-round system will consume this value.
        public int SelectedRoundCount { get; private set; } = 3;
        public bool RandomEventsEnabled { get; private set; }
        public int SelectedRandomEvents { get; private set; } = RandomEventDirector.AllEventsMask;

        public bool TryOpenSetup()
        {
            if (!CanStart || IsSetupOpen) return false;
            IsSetupOpen = true;
            setupSubmitReleased = !IsSetupSubmitHeld();
            setupStartAllowedAt = Time.unscaledTime + 0.2f;
            RefreshLobby();
            return true;
        }

        public bool TryCloseSetup()
        {
            if (!isActiveAndEnabled || !IsLobbyOpen || !IsSetupOpen) return false;
            IsSetupOpen = false;
            RefreshLobby();
            return true;
        }

        public bool TrySetSharedCpuDifficulty(CpuDifficulty difficulty)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen || !IsSetupOpen ||
                difficulty < CpuDifficulty.Noob || difficulty > CpuDifficulty.God) return false;
            SelectedCpuDifficulty = difficulty;
            RefreshLobby();
            return true;
        }

        public bool TrySetRoundCount(int count)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen || !IsSetupOpen || count < 1 || count > 9) return false;
            SelectedRoundCount = count;
            RefreshLobby();
            return true;
        }

        public bool IsRandomEventSelected(RandomEventKind kind) =>
            kind >= RandomEventKind.Earthquake && kind <= RandomEventKind.Lightning &&
            (SelectedRandomEvents & (1 << (int)kind)) != 0;

        public bool TrySetRandomEvent(RandomEventKind kind, bool enabled)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen ||
                kind < RandomEventKind.Earthquake || kind > RandomEventKind.Lightning) return false;
            int bit = 1 << (int)kind;
            SelectedRandomEvents = enabled ? SelectedRandomEvents | bit : SelectedRandomEvents & ~bit;
            RefreshLobby();
            return true;
        }

        public bool TrySetRandomEvents(bool enabled)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen) return false;
            RandomEventsEnabled = enabled;
            RefreshLobby();
            return true;
        }
        public bool IsLobbyOpen => gameManager != null && !gameManager.HasStarted;
        public bool CanStart
        {
            get
            {
                if (!isActiveAndEnabled || !IsLobbyOpen || !gameManager.isActiveAndEnabled || leavingPlayers.Count > 0)
                {
                    return false;
                }

                for (int slot = 0; slot < selectedPlayerCount; slot++)
                {
                    CouchPlayerController player = GetParticipant(slot);
                    if (player == null ? manager.playerPrefab == null : !IsPlayerReady(player))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PlayerInput GetPlayer(int slot)
        {
            return GetParticipant(slot)?.HumanInput;
        }

        public CouchPlayerController GetParticipant(int slot)
        {
            if (slot < 0 || slot >= MaximumPlayers) return null;
            for (int i = 0; i < CouchPlayerController.ActivePlayers.Count; i++)
            {
                CouchPlayerController player = CouchPlayerController.ActivePlayers[i];
                if (player != null && player.PlayerNumber == slot + 1) return player;
            }
            return null;
        }

        public static bool IsReady(CouchPlayerController player)
        {
            return player != null && player.isActiveAndEnabled &&
                (player.IsCpu ? player.Cpu.isActiveAndEnabled : IsConnected(player.HumanInput));
        }

        public bool IsPlayerReady(CouchPlayerController player)
        {
            return IsReady(player) && (player.IsCpu || readyPlayers.Contains(player));
        }

        public bool TrySetPlayerReady(int slot, bool ready)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (!isActiveAndEnabled || !IsLobbyOpen || IsSetupOpen || player == null || player.IsCpu ||
                leavingPlayers.Contains(player) || !IsReady(player))
            {
                return false;
            }

            bool changed = ready ? readyPlayers.Add(player) : readyPlayers.Remove(player);
            if (changed) RefreshLobby();
            return changed;
        }

        public bool TryAddCpu(int slot, CpuDifficulty difficulty = CpuDifficulty.Noob)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen || slot < 0 || slot >= selectedPlayerCount ||
                difficulty < CpuDifficulty.Noob || difficulty > CpuDifficulty.God || GetParticipant(slot) != null ||
                leavingPlayers.Count > 0 || manager.playerPrefab == null)
            {
                return false;
            }

            // Instantiate under an inactive parent so PlayerInput never enables or pairs devices.
            GameObject staging = new("CPU staging");
            staging.SetActive(false);
            GameObject avatar = Instantiate(manager.playerPrefab, staging.transform);
            PlayerInput input = avatar.GetComponent<PlayerInput>();
            CouchPlayerController controller = avatar.GetComponent<CouchPlayerController>();
            if (input == null || controller == null)
            {
                Destroy(staging);
                return false;
            }
            input.enabled = false;
            if (navigation == null) navigation = gameObject.AddComponent<CpuNavigation>();
            if (!avatar.TryGetComponent(out CpuPlayerController cpu)) cpu = avatar.AddComponent<CpuPlayerController>();
            cpu.enabled = true;
            controller.InitializeCpu(slot, cpu);
            cpu.Configure(controller, navigation, gameManager, difficulty);
            avatar.transform.SetParent(null, true);
            avatar.SetActive(true);
            Destroy(staging);
            RefreshLobby();
            return true;
        }

        public bool TrySetCpuDifficulty(int slot, CpuDifficulty difficulty)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (!isActiveAndEnabled || !IsLobbyOpen || player == null || !player.IsCpu ||
                leavingPlayers.Contains(player) || !player.Cpu.SetDifficulty(difficulty)) return false;
            RefreshLobby();
            return true;
        }

        public bool TrySetPlayerColor(int slot, int colorIndex)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (!isActiveAndEnabled || !IsLobbyOpen || player == null || leavingPlayers.Contains(player) ||
                readyPlayers.Contains(player) || !player.TrySetColor(colorIndex)) return false;
            RefreshLobby();
            return true;
        }

        public bool TryCyclePlayerColor(int slot, int direction)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (player == null || (direction != -1 && direction != 1)) return false;
            for (int offset = 1; offset < PlayerColorPalette.Count; offset++)
            {
                int index = (player.ColorIndex + direction * offset + PlayerColorPalette.Count) % PlayerColorPalette.Count;
                if (CouchPlayerController.IsColorAvailable(index, player)) return TrySetPlayerColor(slot, index);
            }
            return false;
        }

        public bool TryCyclePlayerCharacter(int slot, int direction)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (!isActiveAndEnabled || !IsLobbyOpen || player == null || leavingPlayers.Contains(player) ||
                readyPlayers.Contains(player) || (direction != -1 && direction != 1) || player.CharacterCount == 0)
            {
                return false;
            }

            int index = (player.CharacterIndex + direction + player.CharacterCount) % player.CharacterCount;
            if (!player.TrySetCharacter(index)) return false;
            RefreshLobby();
            return true;
        }

        public static bool IsConnected(PlayerInput player)
        {
            return player != null && player.isActiveAndEnabled &&
                player.devices.Count > 0 && !player.hasMissingRequiredDevices;
        }

        public bool CanSelectPlayerCount(int count)
        {
            if (!isActiveAndEnabled || !IsLobbyOpen || count < 1 || count > MaximumPlayers)
            {
                return false;
            }

            for (int slot = count; slot < MaximumPlayers; slot++)
            {
                if (GetParticipant(slot) != null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TrySetPlayerCount(int count)
        {
            if (!CanSelectPlayerCount(count))
            {
                return false;
            }

            selectedPlayerCount = count;
            RefreshLobby();
            return true;
        }

        public bool TryJoin(InputDevice device)
        {
            if (!IsLobbyOpen || IsSetupOpen || !isActiveAndEnabled || device == null || !device.added ||
                !(device is Gamepad || device is Keyboard) || PlayerInput.FindFirstPairedToDevice(device) != null)
            {
                return false;
            }

            if (device is Keyboard)
            {
                foreach (PlayerInput player in PlayerInput.all)
                {
                    if (player.currentControlScheme == "Keyboard&Mouse")
                    {
                        return false;
                    }
                }
            }

            for (int slot = 0; slot < selectedPlayerCount; slot++)
            {
                if (GetParticipant(slot) == null)
                {
                    return manager.JoinPlayer(slot, pairWithDevice: device) != null;
                }
            }

            return false;
        }

        public bool TryLeave(int slot)
        {
            CouchPlayerController player = GetParticipant(slot);
            if (!isActiveAndEnabled || !IsLobbyOpen || IsSetupOpen || player == null || !leavingPlayers.Add(player))
            {
                return false;
            }

            readyPlayers.Remove(player);
            Destroy(player.gameObject);
            RefreshLobby();
            return true;
        }

        public bool TryStartGame()
        {
            if (!CanStart)
            {
                return false;
            }
            if (IsSetupOpen && (!setupSubmitReleased || Time.unscaledTime < setupStartAllowedAt)) return false;
            if (Time.unscaledTime < retryStartAt) return false;
            StartFailure = null;

            for (int slot = 0; slot < selectedPlayerCount; slot++)
            {
                if (GetParticipant(slot) == null && !TryAddCpu(slot,
                    IsSetupOpen ? SelectedCpuDifficulty : CpuDifficulty.Noob))
                {
                    StartFailure = "Could not fill an empty slot with a CPU. Check the player prefab and try again.";
                    retryStartAt = Time.unscaledTime + 1f;
                    LobbyChanged?.Invoke();
                    return false;
                }
            }
            if (!CanStart) return false;

            for (int slot = 0; slot < selectedPlayerCount; slot++)
            {
                CouchPlayerController player = GetParticipant(slot);
                if (IsSetupOpen && player.IsCpu && !player.Cpu.SetDifficulty(SelectedCpuDifficulty)) return false;
                if (player.IsCpu && (!navigation.Build(gameManager.ArenaBounds, player.GetComponent<CharacterController>()) ||
                    !player.Cpu.CanNavigate()))
                {
                    StartFailure = "CPU cannot reach its collector. Check arena navigation and try again.";
                    retryStartAt = Time.unscaledTime + 1f;
                    Debug.LogError("CPU players need a walkable path from their spawn to their collector.", this);
                    LobbyChanged?.Invoke();
                    return false;
                }
            }
            if (RandomEventsEnabled && SelectedRandomEvents != 0)
            {
                if (!gameManager.TryGetComponent(out RandomEventDirector events))
                    events = gameManager.gameObject.AddComponent<RandomEventDirector>();
                if (!events.Configure(gameManager, SelectedRandomEvents))
                {
                    StartFailure = "Random events are unavailable. Disable them or check event assets.";
                    retryStartAt = Time.unscaledTime + 1f;
                    Destroy(events);
                    LobbyChanged?.Invoke();
                    return false;
                }
            }
            if (!gameManager.TryStartRound()) return false;

            IsSetupOpen = false;
            manager.DisableJoining();
            lobbyActions.Disable();
            LobbyChanged?.Invoke();
            return true;
        }

        private void Awake()
        {
            PlayerColorPalette.ConfigureDefaults(defaultPlayerColors);
            manager = GetComponent<PlayerInputManager>();
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersManually;
            manager.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
            manager.splitScreen = false;
            selectedPlayerCount = Mathf.Clamp(selectedPlayerCount, 1, MaximumPlayers);
            if (gameManager == null)
            {
                Debug.LogError("The connection menu requires a GameManager reference.", this);
                enabled = false;
                return;
            }

            lobbyActions = new InputActionMap("Lobby");
            AddButton("Join", "<Keyboard>/enter", "<Gamepad>/buttonSouth", context =>
            {
                if (IsSetupOpen)
                {
                    // UI Toolkit submit acts on the focused selector or Start button.
                    return;
                }
                PlayerInput player = PlayerInput.FindFirstPairedToDevice(context.control.device);
                if (player != null) TrySetPlayerReady(player.playerIndex, true);
                else TryJoin(context.control.device);
            });
            AddButton("Leave", "<Keyboard>/escape", "<Gamepad>/buttonEast", context =>
            {
                if (IsSetupOpen)
                {
                    TryCloseSetup();
                    return;
                }
                PlayerInput player = PlayerInput.FindFirstPairedToDevice(context.control.device);
                if (player != null)
                {
                    CouchPlayerController participant = GetParticipant(player.playerIndex);
                    if (IsPlayerReady(participant)) TrySetPlayerReady(player.playerIndex, false);
                    else TryLeave(player.playerIndex);
                }
            });
            lobbyActions.FindAction("Join").AddBinding("<Keyboard>/numpadEnter");
            AddButton("PreviousColor", "<Keyboard>/q", "<Gamepad>/leftShoulder", context => ChangeDeviceColor(context.control.device, -1));
            AddButton("NextColor", "<Keyboard>/e", "<Gamepad>/rightShoulder", context => ChangeDeviceColor(context.control.device, 1));
            AddButton("PreviousCharacter", "<Keyboard>/z", "<Gamepad>/dpad/down", context => ChangeDeviceCharacter(context.control.device, -1));
            AddButton("NextCharacter", "<Keyboard>/x", "<Gamepad>/dpad/up", context => ChangeDeviceCharacter(context.control.device, 1));
            AddButton("Start", "<Keyboard>/space", "<Gamepad>/start", context =>
            {
                if (IsSetupOpen)
                {
                    TryStartGame();
                }
                else if (PlayerInput.FindFirstPairedToDevice(context.control.device) != null)
                {
                    TryOpenSetup();
                }
            });
        }

        private void OnEnable()
        {
            if (lobbyActions == null)
            {
                return;
            }

            manager.onPlayerJoined += OnPlayerJoined;
            manager.onPlayerLeft += OnPlayerLeft;
            CouchPlayerController.PlayerLeft += OnParticipantLeft;
            leavingPlayers.RemoveWhere(player => player == null || !player.isActiveAndEnabled);
            foreach (PlayerInput player in PlayerInput.all)
            {
                SubscribeDeviceEvents(player);
            }

            if (IsLobbyOpen)
            {
                lobbyActions.Enable();
            }

            RefreshLobby();
        }

        private void Start()
        {
            // Refresh after all scene components have completed Awake/OnEnable.
            RefreshLobby();
        }

        private void Update()
        {
            if (IsSetupOpen && !setupSubmitReleased && !IsSetupSubmitHeld()) setupSubmitReleased = true;
        }

        private static bool IsSetupSubmitHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
                keyboard.spaceKey.isPressed)) return true;
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.startButton.isPressed || gamepad.buttonSouth.isPressed) return true;
            }
            return false;
        }

        private void OnDisable()
        {
            lobbyActions?.Disable();
            if (manager == null)
            {
                return;
            }

            manager.DisableJoining();
            manager.onPlayerJoined -= OnPlayerJoined;
            manager.onPlayerLeft -= OnPlayerLeft;
            CouchPlayerController.PlayerLeft -= OnParticipantLeft;
            foreach (PlayerInput player in PlayerInput.all)
            {
                UnsubscribeDeviceEvents(player);
            }
        }

        private void OnDestroy()
        {
            lobbyActions?.Dispose();
        }

        private void AddButton(string name, string keyboard, string gamepad, Action<InputAction.CallbackContext> callback)
        {
            // Pass-through handles simultaneous presses from different devices independently.
            InputAction action = lobbyActions.AddAction(name, InputActionType.PassThrough);
            action.AddBinding(keyboard);
            action.AddBinding(gamepad);
            action.performed += context =>
            {
                if (IsLobbyOpen && context.ReadValue<float>() > 0.5f)
                {
                    callback(context);
                }
            };
        }

        private void OnPlayerJoined(PlayerInput player)
        {
            CouchPlayerController occupant = GetParticipant(player.playerIndex);
            if (occupant != null && occupant.gameObject != player.gameObject)
            {
                Destroy(player.gameObject);
                return;
            }
            if (!player.TryGetComponent(out CouchPlayerController controller))
            {
                Debug.LogError("Joined player is missing a CouchPlayerController.", player);
                return;
            }

            controller.InitializePlayer();
            SubscribeDeviceEvents(player);
            RefreshLobby();
            // PlayerInput may announce its join before the controller finishes enabling.
            refreshPending = true;
        }

        private void ChangeDeviceColor(InputDevice device, int direction)
        {
            PlayerInput player = PlayerInput.FindFirstPairedToDevice(device);
            if (player != null) TryCyclePlayerColor(player.playerIndex, direction);
        }

        private void ChangeDeviceCharacter(InputDevice device, int direction)
        {
            PlayerInput player = PlayerInput.FindFirstPairedToDevice(device);
            if (player != null) TryCyclePlayerCharacter(player.playerIndex, direction);
        }

        private void OnPlayerLeft(PlayerInput player)
        {
            UnsubscribeDeviceEvents(player);
            refreshPending = true;
        }

        private void OnParticipantLeft(CouchPlayerController player)
        {
            readyPlayers.Remove(player);
            leavingPlayers.Remove(player);
            refreshPending = true;
        }

        private void SubscribeDeviceEvents(PlayerInput player)
        {
            player.onDeviceLost += OnDeviceChanged;
            player.onDeviceRegained += OnDeviceChanged;
        }

        private void UnsubscribeDeviceEvents(PlayerInput player)
        {
            player.onDeviceLost -= OnDeviceChanged;
            player.onDeviceRegained -= OnDeviceChanged;
        }

        private void OnDeviceChanged(PlayerInput player)
        {
            // InputUser can notify before its paired-device list has finished updating.
            refreshPending = true;
        }

        private void LateUpdate()
        {
            if (refreshPending)
            {
                refreshPending = false;
                readyPlayers.RemoveWhere(player => player == null || !IsReady(player));
                RefreshLobby();
            }
        }

        private void RefreshLobby()
        {
            StartFailure = null;
            retryStartAt = 0f;
            bool hasEmptySlot = false;
            for (int slot = 0; slot < selectedPlayerCount; slot++)
            {
                if (GetParticipant(slot) == null) hasEmptySlot = true;
            }
            if (IsLobbyOpen && !IsSetupOpen && hasEmptySlot)
            {
                manager.EnableJoining();
            }
            else
            {
                manager.DisableJoining();
            }

            LobbyChanged?.Invoke();
        }
    }
}
