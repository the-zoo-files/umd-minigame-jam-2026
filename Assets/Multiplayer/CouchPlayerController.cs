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
        private Material bodyMaterial;
        private Material directionMaterial;
        private bool initializationAttempted;
        private bool pickupConfigured;

        public int PlayerNumber { get; private set; }
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
        }

        private void Start()
        {
            InitializePlayer();
        }

        public void InitializePlayer()
        {
            if (initializationAttempted)
            {
                return;
            }

            initializationAttempted = true;
            // PlayerInput completes device pairing and action cloning in OnEnable.
            // Initialize from its joined callback, with Start as the standalone fallback.
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            moveAction = playerInput.actions?.FindAction("Player/Move");
            attackAction = playerInput.actions?.FindAction("Player/Attack");
            if (moveAction == null || attackAction == null ||
                playerInput.playerIndex < 0 || playerInput.playerIndex >= SpawnPositions.Length)
            {
                Debug.LogError("Player requires Move and Attack actions and a player index from 0 to 3.", this);
                enabled = false;
                return;
            }

            pickupConfigured = holdPoint != null;
            if (!pickupConfigured)
            {
                Debug.LogError("Player prefab is missing its flask hold point. Pickup is disabled.", this);
            }

            int slot = playerInput.playerIndex;
            PlayerNumber = slot + 1;
            PlaceAtSpawn(slot);
            gameObject.name = DisplayName;

            Renderer playerRenderer = GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                bodyMaterial = playerRenderer.material;
                bodyMaterial.color = PlayerColors[slot];
            }

            Transform directionGizmo = transform.Find("DirectionGizmo");
            if (directionGizmo != null && directionGizmo.TryGetComponent(out Renderer gizmoRenderer))
            {
                directionMaterial = gizmoRenderer.material;
                directionMaterial.color = PlayerColors[slot];
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

            if (moveAction == null || attackAction == null)
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
            TryPickUp(hit.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPickUp(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryPickUp(other);
        }

        private void TryPickUp(Collider other)
        {
            if (!isActiveAndEnabled || !pickupConfigured || holdPoint == null || heldFlask != null)
            {
                return;
            }

            PickupFlask flask = other.GetComponentInParent<PickupFlask>();
            if (flask != null && flask.TryPickUp(holdPoint))
            {
                heldFlask = flask;
            }
        }

        private void ThrowHeldFlask()
        {
            if (heldFlask == null)
            {
                return;
            }

            PickupFlask flaskToThrow = heldFlask;
            Vector3 origin = throwPoint != null ? throwPoint.position : flaskToThrow.transform.position;
            if (PlayerFlaskCollector.TryGetNearby(PlayerNumber, transform.position, out PlayerFlaskCollector collector))
            {
                if (flaskToThrow.TryThrowDirectly(
                    collector.CollectionPoint,
                    PlayerNumber,
                    directThrowDuration,
                    () =>
                    {
                        if (collector != null)
                        {
                            collector.Collect(flaskToThrow);
                        }
                    }))
                {
                    heldFlask = null;
                }

                return;
            }

            Vector3 target = origin + transform.forward * throwDistance;
            target.y = throwLandingHeight;
            if (!Ballistics.TryCalculateVelocity(origin, target, throwApexHeight, out Vector3 velocity))
            {
                Debug.LogError("Cannot throw flask: check trajectory settings and downward-only gravity.", this);
                return;
            }

            if (flaskToThrow.TryThrow(velocity, PlayerNumber))
            {
                if (throwPoint != null)
                {
                    flaskToThrow.transform.SetPositionAndRotation(
                        origin,
                        Quaternion.LookRotation(transform.forward, Vector3.up));
                }

                heldFlask = null;
            }
        }

        private void OnDestroy()
        {
            if (bodyMaterial != null)
            {
                Destroy(bodyMaterial);
            }

            if (directionMaterial != null)
            {
                Destroy(directionMaterial);
            }

            if (!activePlayers.Remove(this))
            {
                return;
            }

            PlayerLeft?.Invoke(this);
        }
    }
}
