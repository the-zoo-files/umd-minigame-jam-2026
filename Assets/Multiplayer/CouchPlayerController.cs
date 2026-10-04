using System;
using System.Collections.Generic;
using UmdJam.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UmdJam.Multiplayer
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
    public sealed class CouchPlayerController : MonoBehaviour
    {
        private static readonly Vector3[] SpawnPositions =
        {
            new(-6f, 1f, 6f),
            new(6f, 1f, 6f),
            new(6f, 1f, -6f),
            new(-6f, 1f, -6f)
        };

        private static readonly Color[] PlayerColors =
        {
            new(0.22f, 0.78f, 1f),
            new(1f, 0.38f, 0.46f),
            new(1f, 0.82f, 0.25f),
            new(0.46f, 0.9f, 0.42f)
        };

        public static event Action<CouchPlayerController> PlayerJoined;
        public static event Action<CouchPlayerController> PlayerLeft;
        public static event Action<CouchPlayerController> ScoreChanged;

        public static IReadOnlyList<CouchPlayerController> ActivePlayers => activePlayers;

        private static readonly List<CouchPlayerController> activePlayers = new();

        [SerializeField] private float moveSpeed = 9.5f;
        [SerializeField] private float turnSpeed = 30f;
        [SerializeField] private float throwDistance = 8f;
        [SerializeField] private float throwApexHeight = 3f;
        [SerializeField] private float throwLandingHeight = 0.35f;
        [SerializeField, Min(0f)] private float directThrowDuration = 0.2f;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Transform throwPoint;

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction attackAction;
        private PickupFlask heldFlask;

        public int PlayerNumber => playerInput.playerIndex + 1;
        public string DisplayName => $"Player {PlayerNumber}";
        public int Score { get; private set; }

        public static void AddScore(int playerNumber, int points)
        {
            if (points > 0)
            {
                ChangeScore(playerNumber, points);
            }
        }

        public static void ChangeScore(int playerNumber, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            foreach (CouchPlayerController player in activePlayers)
            {
                if (player != null && player.PlayerNumber == playerNumber)
                {
                    player.Score += amount;
                    ScoreChanged?.Invoke(player);
                    return;
                }
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            moveAction = playerInput.actions.FindAction("Player/Move", true);
            attackAction = playerInput.actions.FindAction("Player/Attack", true);

            int slot = Mathf.Clamp(playerInput.playerIndex, 0, SpawnPositions.Length - 1);
            PlaceAtSpawn(slot);
            gameObject.name = DisplayName;

            Renderer playerRenderer = GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                playerRenderer.material.color = PlayerColors[slot];
            }

            Transform directionGizmo = transform.Find("DirectionGizmo");
            if (directionGizmo != null && directionGizmo.TryGetComponent(out Renderer gizmoRenderer))
            {
                gizmoRenderer.material.color = PlayerColors[slot];
            }

            if (!activePlayers.Contains(this))
            {
                activePlayers.Add(this);
                PlayerJoined?.Invoke(this);
            }
        }

        public void PlaceAtSpawn(int playerIndex)
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            int slot = Mathf.Clamp(playerIndex, 0, SpawnPositions.Length - 1);
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = SpawnPositions[slot];
            characterController.enabled = wasEnabled;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsRoundOver)
            {
                return;
            }

            Vector2 input = moveAction.ReadValue<Vector2>();
            Vector3 movement = new(input.x, 0f, input.y);
            characterController.SimpleMove(movement * moveSpeed);

            if (movement.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
            }

            if (attackAction.WasPressedThisFrame())
            {
                ThrowHeldFlask();
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            TryPickUp(hit.collider.GetComponentInParent<PickupFlask>());
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickUp(other.GetComponentInParent<PickupFlask>());
        }

        private void OnTriggerStay(Collider other)
        {
            TryPickUp(other.GetComponentInParent<PickupFlask>());
        }

        private void TryPickUp(PickupFlask flask)
        {
            if (heldFlask != null || flask == null || flask.IsHeld || flask.IsCollected)
            {
                return;
            }

            if (holdPoint == null)
            {
                Debug.LogError("Player prefab is missing its flask hold point.", this);
                return;
            }

            heldFlask = flask;
            heldFlask.PickUp(holdPoint);
        }

        private void ThrowHeldFlask()
        {
            if (heldFlask == null)
            {
                return;
            }

            PickupFlask flaskToThrow = heldFlask;
            heldFlask = null;

            if (throwPoint != null)
            {
                flaskToThrow.transform.SetPositionAndRotation(
                    throwPoint.position,
                    Quaternion.LookRotation(transform.forward, Vector3.up));
            }

            Vector3 origin = flaskToThrow.transform.position;
            if (PlayerFlaskCollector.TryGetNearby(PlayerNumber, transform.position, out PlayerFlaskCollector collector))
            {
                flaskToThrow.ThrowDirectly(
                    collector.CollectionPoint,
                    PlayerNumber,
                    directThrowDuration,
                    () =>
                    {
                        if (collector != null)
                        {
                            collector.Collect(flaskToThrow);
                        }
                    });
                return;
            }

            Vector3 target = origin + transform.forward * throwDistance;
            target.y = throwLandingHeight;
            flaskToThrow.Throw(CalculateBallisticVelocity(origin, target), PlayerNumber);
        }

        private Vector3 CalculateBallisticVelocity(Vector3 origin, Vector3 target)
        {
            float gravity = Mathf.Abs(Physics.gravity.y);
            float verticalSpeed = Mathf.Sqrt(2f * gravity * throwApexHeight);
            float apexY = origin.y + throwApexHeight;
            float riseTime = verticalSpeed / gravity;
            float fallTime = Mathf.Sqrt(2f * Mathf.Max(0.01f, apexY - target.y) / gravity);
            float flightTime = riseTime + fallTime;

            Vector3 horizontalDisplacement = target - origin;
            horizontalDisplacement.y = 0f;
            return horizontalDisplacement / flightTime + Vector3.up * verticalSpeed;
        }

        private void OnDestroy()
        {
            if (!activePlayers.Remove(this))
            {
                return;
            }

            PlayerLeft?.Invoke(this);
        }
    }
}
