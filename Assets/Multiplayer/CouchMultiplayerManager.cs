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
        private readonly HashSet<PlayerInput> leavingPlayers = new();

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
                    if (!IsConnected(GetPlayer(slot)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public PlayerInput GetPlayer(int slot)
        {
            return PlayerInput.GetPlayerByIndex(slot);
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
                if (GetPlayer(slot) != null)
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
                if (GetPlayer(slot) == null)
                {
                    return manager.JoinPlayer(slot, pairWithDevice: device) != null;
                }
            }

            return false;
        }

        public bool TryLeave(int slot)
        {
            PlayerInput player = GetPlayer(slot);
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
            if (!CanStart || !gameManager.TryStartRound())
            {
                return false;
            }

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
            if (!player.TryGetComponent(out CouchPlayerController controller))
            {
                Debug.LogError("Joined player is missing a CouchPlayerController.", player);
                return;
            }

            controller.InitializePlayer();
            SubscribeDeviceEvents(player);
            RefreshLobby();
        }

        private void OnPlayerLeft(PlayerInput player)
        {
            leavingPlayers.Remove(player);
            UnsubscribeDeviceEvents(player);
            RefreshLobby();
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
            if (IsLobbyOpen && PlayerInput.all.Count < selectedPlayerCount)
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
