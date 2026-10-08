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

        public static event Action<CouchPlayerController> PlayerJoined;
        public static event Action<CouchPlayerController> PlayerLeft;
        public static event Action<CouchPlayerController> ScoreChanged;
        public static event Action<CouchPlayerController> ColorChanged;
        public static event Action<CouchPlayerController> CharacterChanged;

        public static IReadOnlyList<CouchPlayerController> ActivePlayers => activePlayers;

        private static readonly List<CouchPlayerController> activePlayers = new();

        [SerializeField] private float moveSpeed = 9.5f;
        [SerializeField] private float turnSpeed = 30f;
        [SerializeField] private float throwDistance = 8f;
        [SerializeField] private float throwApexHeight = 3f;
        [SerializeField] private float throwLandingHeight = 0.35f;
        [SerializeField, Min(0f)] private float directThrowDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float throwReleaseNormalizedTime = 0.43f;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Transform throwPoint;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private CharacterSkinCatalog characterSkins;

        private CharacterController characterController;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction attackAction;
        private PickupFlask heldFlask;
        private Material bodyMaterial;
        private Material directionMaterial;
        private bool initializationAttempted;
        private bool pickupConfigured;
        private CpuPlayerController cpu;
        private GameObject activeCharacter;
        private Animator characterAnimator;
        private bool throwPending;
        private bool throwAnimationStarted;
        private bool characterSetupPending;

        private static readonly int SpeedAnimation = Animator.StringToHash("Speed");
        private static readonly int CarryingAnimation = Animator.StringToHash("Carrying");
        private static readonly int ThrowAnimation = Animator.StringToHash("Throw");
        private static readonly int ThrowState = Animator.StringToHash("Base Layer.Throw");

        public int PlayerNumber { get; private set; }
        public bool IsCpu => cpu != null;
        public CpuPlayerController Cpu => cpu;
        public PlayerInput HumanInput => IsCpu ? null : playerInput;
        public float MoveSpeed => moveSpeed;
        public bool IsCarrying => heldFlask != null;
        public string DisplayName => IsCpu ? $"CPU {PlayerNumber}" : $"Player {PlayerNumber}";
        public int Score { get; private set; }
        public int ColorIndex { get; private set; } = -1;
        public Color PlayerColor => PlayerColorPalette.Get(ColorIndex).Color;
        public int CharacterIndex { get; private set; }
        public int CharacterCount => characterSkins != null ? characterSkins.Count : 0;
        public string CharacterName => characterSkins?.Get(CharacterIndex)?.DisplayName ?? "Character";

        public static Vector3 GetSpawnPosition(int slot) => SpawnPositions[slot];

        public static bool IsColorAvailable(int index, CouchPlayerController owner = null)
        {
            if (!PlayerColorPalette.IsValid(index)) return false;
            foreach (CouchPlayerController player in activePlayers)
            {
                // Disconnected and pending-leave participants keep their colors until teardown.
                if (player != null && player != owner && player.ColorIndex == index) return false;
            }
            return true;
        }

        internal bool TrySetColor(int index)
        {
            if (PlayerNumber == 0 || !isActiveAndEnabled ||
                (GameManager.Instance != null && GameManager.Instance.HasStarted) || !IsColorAvailable(index, this))
            {
                return false;
            }
            if (ColorIndex == index) return true;
            ColorIndex = index;
            ApplyColor();
            ColorChanged?.Invoke(this);
            return true;
        }

        internal bool TrySetCharacter(int index)
        {
            if (PlayerNumber == 0 || !isActiveAndEnabled || index < 0 || index >= CharacterCount ||
                (GameManager.Instance != null && GameManager.Instance.HasStarted))
            {
                return false;
            }
            if (CharacterIndex == index && activeCharacter != null) return true;
            CharacterIndex = index;
            ApplyCharacter();
            CharacterChanged?.Invoke(this);
            return activeCharacter != null;
        }

        public void InitializeCpu(int slot, CpuPlayerController driver)
        {
            if (initializationAttempted || slot < 0 || slot >= SpawnPositions.Length || driver == null)
            {
                return;
            }

            initializationAttempted = true;
            cpu = driver;
            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();
            playerInput.enabled = false;
            InitializeIdentity(slot);
        }

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

            InitializeIdentity(playerInput.playerIndex);
        }

        private void InitializeIdentity(int slot)
        {
            PlayerNumber = slot + 1;
            int preferred = PlayerColorPalette.DefaultForSlot(slot);
            for (int offset = 0; offset < PlayerColorPalette.Count; offset++)
            {
                int candidate = (preferred + offset) % PlayerColorPalette.Count;
                if (!IsColorAvailable(candidate, this)) continue;
                ColorIndex = candidate;
                break;
            }
            if (ColorIndex < 0)
            {
                Debug.LogError("No unique player color is available.", this);
                enabled = false;
                return;
            }
            PlaceAtSpawn(slot);
            gameObject.name = DisplayName;

            Renderer playerRenderer = GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                bodyMaterial = playerRenderer.material;
            }

            Transform directionGizmo = transform.Find("DirectionGizmo");
            if (directionGizmo != null && directionGizmo.TryGetComponent(out Renderer gizmoRenderer))
            {
                directionMaterial = gizmoRenderer.material;
            }
            ApplyColor();
            ApplyCharacter();

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

        private void ApplyColor()
        {
            if (bodyMaterial != null) bodyMaterial.color = PlayerColor;
            if (directionMaterial != null) directionMaterial.color = PlayerColor;
        }

        private void ApplyCharacter()
        {
            if (activeCharacter != null)
            {
                if (holdPoint != null && holdPoint.IsChildOf(activeCharacter.transform))
                {
                    holdPoint.SetParent(characterRoot, false);
                }
                activeCharacter.SetActive(false);
                Destroy(activeCharacter);
            }

            characterAnimator = null;
            CharacterSkinCatalog.Entry skin = characterSkins?.Get(CharacterIndex);
            if (skin?.Prefab == null || characterRoot == null)
            {
                pickupConfigured = false;
                Debug.LogError("Player prefab requires a character root and a valid default character skin.", this);
                return;
            }

            activeCharacter = Instantiate(skin.Prefab, characterRoot);
            activeCharacter.name = skin.DisplayName;
            Transform visual = activeCharacter.transform;
            visual.SetLocalPositionAndRotation(skin.LocalPosition, Quaternion.Euler(skin.LocalEulerAngles));
            visual.localScale = skin.LocalScale;

            foreach (Collider visualCollider in activeCharacter.GetComponentsInChildren<Collider>(true))
            {
                visualCollider.enabled = false;
            }

            if (skin.MaterialOverride != null)
            {
                foreach (Renderer visualRenderer in activeCharacter.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = visualRenderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = skin.MaterialOverride;
                    visualRenderer.sharedMaterials = materials;
                }
            }

            characterAnimator = activeCharacter.GetComponent<Animator>();
            if (characterAnimator == null) characterAnimator = activeCharacter.AddComponent<Animator>();
            characterAnimator.runtimeAnimatorController = characterSkins.AnimationController;
            characterAnimator.avatar = skin.Avatar;
            characterAnimator.applyRootMotion = false;
            CharacterAnimationEvents animationEvents =
                characterAnimator.GetComponent<CharacterAnimationEvents>() ??
                characterAnimator.gameObject.AddComponent<CharacterAnimationEvents>();
            animationEvents.Configure(this);
            characterSetupPending = true;
            ConfigureCharacterRuntime();
        }

        private void Update()
        {
            if (characterSetupPending) ConfigureCharacterRuntime();
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                return;
            }

            Vector2 input;
            bool attack;
            if (cpu != null)
            {
                cpu.ReadCommand(out input, out attack);
            }
            else if (moveAction != null && attackAction != null)
            {
                input = moveAction.ReadValue<Vector2>();
                attack = attackAction.WasPressedThisFrame();
            }
            else
            {
                return;
            }

            Vector3 movement = new(input.x, 0f, input.y);
            characterController.SimpleMove(movement * moveSpeed);
            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedAnimation, Mathf.Clamp01(input.magnitude));
                characterAnimator.SetBool(CarryingAnimation, heldFlask != null);
            }

            if (movement.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movement);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    turnSpeed * Time.deltaTime);
            }

            if (attack)
            {
                BeginThrow();
            }

            TryReleasePendingThrow();
        }

        private void ConfigureCharacterRuntime()
        {
            if (characterAnimator == null || !characterAnimator.isActiveAndEnabled) return;

            characterAnimator.Rebind();
            characterAnimator.SetBool(CarryingAnimation, heldFlask != null);
            Transform rightHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            pickupConfigured = holdPoint != null && rightHand != null;
            if (pickupConfigured)
            {
                holdPoint.SetParent(rightHand, false);
            }
            else
            {
                Debug.LogError("Player prefab is missing its flask hold point. Pickup is disabled.", this);
            }
            characterSetupPending = false;
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
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            {
                return;
            }

            if (!isActiveAndEnabled || !pickupConfigured || holdPoint == null || heldFlask != null)
            {
                return;
            }

            PickupFlask flask = other.GetComponentInParent<PickupFlask>();
            if (flask != null && flask.TryPickUp(holdPoint))
            {
                heldFlask = flask;
                if (characterAnimator != null) characterAnimator.SetBool(CarryingAnimation, true);
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
                if (collector.TryTransfer(flaskToThrow, directThrowDuration))
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

        private void BeginThrow()
        {
            if (heldFlask == null || throwPending) return;
            if (characterAnimator == null || characterAnimator.runtimeAnimatorController == null)
            {
                ThrowHeldFlask();
                return;
            }

            throwPending = true;
            throwAnimationStarted = false;
            characterAnimator.SetBool(CarryingAnimation, false);
            characterAnimator.ResetTrigger(ThrowAnimation);
            characterAnimator.SetTrigger(ThrowAnimation);
        }

        internal void ReleaseFlaskFromAnimation()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (!throwPending) return;
            throwPending = false;
            throwAnimationStarted = false;
            ThrowHeldFlask();
        }

        private void TryReleasePendingThrow()
        {
            if (!throwPending || characterAnimator == null) return;

            AnimatorStateInfo state = characterAnimator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != ThrowState && characterAnimator.IsInTransition(0))
            {
                state = characterAnimator.GetNextAnimatorStateInfo(0);
            }

            if (state.fullPathHash != ThrowState)
            {
                // Do not strand a held flask if an interrupted transition skips the event.
                if (throwAnimationStarted) ReleaseFlaskFromAnimation();
                return;
            }

            throwAnimationStarted = true;
            if (state.normalizedTime >= throwReleaseNormalizedTime)
            {
                ReleaseFlaskFromAnimation();
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
