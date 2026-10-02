using UnityEngine;
using UnityEngine.InputSystem;

namespace UmdJam.Multiplayer
{
    [RequireComponent(typeof(PlayerInputManager))]
    public sealed class CouchMultiplayerManager : MonoBehaviour
    {
        private PlayerInputManager manager;

        private void Awake()
        {
            manager = GetComponent<PlayerInputManager>();
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersWhenButtonIsPressed;
            manager.splitScreen = false;
            manager.onPlayerJoined += OnPlayerJoined;
            manager.EnableJoining();
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.onPlayerJoined -= OnPlayerJoined;
            }
        }

        private static void OnPlayerJoined(PlayerInput playerInput)
        {
            CouchPlayerController controller = playerInput.GetComponent<CouchPlayerController>();
            if (controller == null)
            {
                Debug.LogError("Joined player is missing a CouchPlayerController.", playerInput);
                return;
            }

            controller.PlaceAtSpawn(playerInput.playerIndex);
        }
    }
}
