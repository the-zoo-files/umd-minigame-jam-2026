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
        [SerializeField, Range(1, MaximumPlayers)] private int selectedPlayerCount = 2;

        private PlayerInputManager manager;
        private InputActionMap lobbyActions;
        private bool refreshPending;
        private readonly HashSet<CouchPlayerController> leavingPlayers = new();
        private CpuNavigation navigation;
        private float retryStartAt;

        public string StartFailure { get; private set; }

        public int SelectedPlayerCount => selectedPlayerCount;
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
                    if (!IsReady(GetParticipant(slot)))
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

        public bool TryAddCpu(int slot, CpuDifficulty difficulty = CpuDifficulty.Pro)
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
                !player.TrySetColor(colorIndex)) return false;
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
            if (!IsLobbyOpen || !isActiveAndEnabled || device == null || !device.added ||
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
            if (!isActiveAndEnabled || !IsLobbyOpen || player == null || !leavingPlayers.Add(player))
            {
                return false;
            }

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
            if (Time.unscaledTime < retryStartAt) return false;
            StartFailure = null;

            for (int slot = 0; slot < selectedPlayerCount; slot++)
            {
                CouchPlayerController player = GetParticipant(slot);
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
            if (!gameManager.TryStartRound()) return false;

            manager.DisableJoining();
            lobbyActions.Disable();
            LobbyChanged?.Invoke();
            return true;
        }

        private void Awake()
        {
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
            AddButton("Join", "<Keyboard>/enter", "<Gamepad>/buttonSouth", context => TryJoin(context.control.device));
            AddButton("Leave", "<Keyboard>/escape", "<Gamepad>/buttonEast", context =>
            {
                PlayerInput player = PlayerInput.FindFirstPairedToDevice(context.control.device);
                if (player != null)
                {
                    TryLeave(player.playerIndex);
                }
            });
            AddButton("Previous", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left", _ => TrySetPlayerCount(selectedPlayerCount - 1));
            AddButton("Next", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right", _ => TrySetPlayerCount(selectedPlayerCount + 1));
            AddButton("PreviousColor", "<Keyboard>/q", "<Gamepad>/leftShoulder", context => ChangeDeviceColor(context.control.device, -1));
            AddButton("NextColor", "<Keyboard>/e", "<Gamepad>/rightShoulder", context => ChangeDeviceColor(context.control.device, 1));
            AddButton("Start", "<Keyboard>/space", "<Gamepad>/start", context =>
            {
                if (PlayerInput.FindFirstPairedToDevice(context.control.device) != null)
                {
                    TryStartGame();
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

        private void OnPlayerLeft(PlayerInput player)
        {
            UnsubscribeDeviceEvents(player);
            refreshPending = true;
        }

        private void OnParticipantLeft(CouchPlayerController player)
        {
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
            if (IsLobbyOpen && hasEmptySlot)
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
