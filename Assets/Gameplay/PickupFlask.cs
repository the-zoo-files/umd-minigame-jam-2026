using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

namespace UmdJam.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PickupFlask : MonoBehaviour
    {
        private static readonly List<PickupFlask> activeFlasks = new();
        public static IReadOnlyList<PickupFlask> ActiveFlasks => activeFlasks;
        [SerializeField] private Flask flask;

        private Rigidbody body;
        private Collider[] flaskColliders;
        private Transform currentHoldPoint;
        private RigidbodyInterpolation freeInterpolation;
        private bool freeUseGravity;
        private int bounceCount;
        private int lastThrowerPlayerNumber;
        private bool isCollected;
        private MachineFlaskShooter poolOwner;
        private Flask poolDefinition;
        private Vector3 spawnScale;
        private bool[] spawnColliderStates;
        private bool isTransferring;
        private Vector3 transferOrigin;
        private Quaternion transferRotation;
        private Vector3 transferTarget;
        private float transferDuration;
        private float transferElapsed;
        private Action transferCallback;
        private Action<PickupFlask> transferReceiverCallback;
        private Behaviour transferReceiver;
        private bool requiresReceiver;

        private float MaximumFallSpeed => flask != null ? flask.MaximumFallSpeed : 14f;
        private float ImpactSpinBoost => flask != null ? flask.ImpactSpinBoost : 2.5f;
        private float FirstBounceSpeed => flask != null ? flask.FirstBounceSpeed : 1.5f;
        private int MaximumBounces => flask != null ? flask.MaximumBounces : 1;
        private float ThrownSpinSpeed => flask != null ? flask.ThrownSpinSpeed : 8f;

        public bool IsHeld { get; private set; }
        public bool IsCollected => isCollected;
        public int LastThrowerPlayerNumber => lastThrowerPlayerNumber;
        public int PointValue => flask != null ? flask.Points : 1;
        public bool IsAvailable => isActiveAndEnabled && !IsHeld && !isCollected && body != null &&
            !body.isKinematic && body.detectCollisions;
        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
        public bool UsesGravity => body != null && body.useGravity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activeFlasks.Clear();

        private void OnEnable()
        {
            if (!activeFlasks.Contains(this)) activeFlasks.Add(this);
        }

        private void OnDisable()
        {
            activeFlasks.Remove(this);
            CancelTransfer();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            flaskColliders = GetComponentsInChildren<Collider>();
            spawnScale = transform.localScale;
            spawnColliderStates = new bool[flaskColliders.Length];
            for (int i = 0; i < flaskColliders.Length; i++)
            {
                spawnColliderStates[i] = flaskColliders[i].enabled;
            }
            ApplyDefinition();
        }

        internal void BindPool(MachineFlaskShooter owner, Flask definition)
        {
            poolOwner = owner;
            poolDefinition = definition;
        }

        internal void ResetForSpawn(Flask definition, Vector3 position, Quaternion rotation)
        {
            ClearTransfer();
            IsHeld = false;
            isCollected = false;
            currentHoldPoint = null;
            lastThrowerPlayerNumber = 0;
            bounceCount = 0;
            transform.SetParent(null, false);
            transform.localScale = spawnScale;
            transform.SetPositionAndRotation(position, rotation);
            body.isKinematic = false;
            body.detectCollisions = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Initialize(definition);
            body.position = position;
            body.rotation = rotation;
            for (int i = 0; i < flaskColliders.Length; i++)
            {
                flaskColliders[i].enabled = spawnColliderStates[i];
            }
            gameObject.SetActive(true);
            body.WakeUp();
        }

        private void LateUpdate()
        {
            if (!IsHeld || currentHoldPoint == null)
            {
                return;
            }

            transform.SetPositionAndRotation(currentHoldPoint.position, currentHoldPoint.rotation);
        }

        private void Update()
        {
            if (!isTransferring) return;
            if (requiresReceiver && (transferReceiver == null || !transferReceiver.isActiveAndEnabled))
            {
                CancelTransfer();
                return;
            }

            transferElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(transferElapsed / transferDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transferOrigin, transferTarget, easedProgress),
                Quaternion.AngleAxis(360f * easedProgress, Vector3.up) * transferRotation);
            if (transferElapsed >= transferDuration) CompleteTransfer();
        }

        private void FixedUpdate()
        {
            if (IsHeld || body.isKinematic || body.linearVelocity.y >= -MaximumFallSpeed)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = -MaximumFallSpeed;
            body.linearVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsHeld || collision.relativeVelocity.sqrMagnitude < 9f)
            {
                return;
            }

            body.AddTorque(Random.onUnitSphere * ImpactSpinBoost, ForceMode.VelocityChange);

            if (bounceCount >= MaximumBounces || collision.contactCount == 0 || collision.GetContact(0).normal.y < 0.55f)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = FirstBounceSpeed * Mathf.Pow(0.55f, bounceCount);
            body.linearVelocity = velocity;
            bounceCount++;
        }

        public bool TryPickUp(Transform holdPoint)
        {
            if (isCollected || IsHeld || isTransferring || holdPoint == null || !isActiveAndEnabled ||
                holdPoint == transform || holdPoint.IsChildOf(transform))
            {
                return false;
            }

            IsHeld = true;
            currentHoldPoint = holdPoint;
            bounceCount = 0;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            body.isKinematic = true;
            body.useGravity = false;

            foreach (Collider flaskCollider in flaskColliders)
            {
                flaskCollider.enabled = false;
            }

            // Humanoid imports commonly carry a large scale on their bone hierarchy.
            // Preserve the flask's authored world scale while attaching it to the hand.
            transform.SetParent(holdPoint, true);
            transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);
            return true;
        }

        public bool TryThrow(Vector3 launchVelocity, int playerNumber = 0)
        {
            if (!IsHeld || !isActiveAndEnabled || !Ballistics.IsFinite(launchVelocity))
            {
                return false;
            }

            bounceCount = 0;
            currentHoldPoint = null;
            transform.SetParent(null, true);

            foreach (Collider flaskCollider in flaskColliders)
            {
                flaskCollider.enabled = true;
            }

            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = freeUseGravity;
            body.interpolation = freeInterpolation;
            body.linearVelocity = launchVelocity;
            body.angularVelocity = Random.onUnitSphere * ThrownSpinSpeed;
            lastThrowerPlayerNumber = playerNumber;
            IsHeld = false;
            return true;
        }

        public bool TryThrowDirectly(Vector3 target, int playerNumber, float duration, Action onArrived)
        {
            if (!BeginTransfer(target, playerNumber, duration)) return false;
            transferCallback = onArrived;
            if (duration == 0f) CompleteTransfer();
            return true;
        }

        public bool TryThrowDirectly(Vector3 target, int playerNumber, float duration,
            Behaviour receiver, Action<PickupFlask> onArrived)
        {
            if (receiver == null || !receiver.isActiveAndEnabled || onArrived == null ||
                !BeginTransfer(target, playerNumber, duration)) return false;
            transferReceiver = receiver;
            requiresReceiver = true;
            transferReceiverCallback = onArrived;
            if (duration == 0f) CompleteTransfer();
            return true;
        }

        private bool BeginTransfer(Vector3 target, int playerNumber, float duration)
        {
            if (!IsHeld || !isActiveAndEnabled || !Ballistics.IsFinite(target) ||
                playerNumber <= 0 || float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f)
            {
                return false;
            }

            bounceCount = 0;
            currentHoldPoint = null;
            transform.SetParent(null, true);
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            body.isKinematic = true;
            body.useGravity = false;

            foreach (Collider flaskCollider in flaskColliders)
            {
                flaskCollider.enabled = false;
            }

            lastThrowerPlayerNumber = playerNumber;
            IsHeld = false;
            transferOrigin = transform.position;
            transferRotation = transform.rotation;
            transferTarget = target;
            transferDuration = duration;
            transferElapsed = 0f;
            isTransferring = true;
            return true;
        }

        public void Initialize(Flask definition)
        {
            flask = definition;
            ApplyDefinition();
        }

        public bool TryCollect(int playerNumber, out int points)
        {
            points = 0;
            if (isCollected || IsHeld || !isActiveAndEnabled || playerNumber <= 0 || lastThrowerPlayerNumber != playerNumber)
            {
                return false;
            }

            isCollected = true;
            ClearTransfer();
            points = PointValue;
            if (poolOwner != null)
            {
                poolOwner.ReturnToPool(this, poolDefinition);
            }
            else
            {
                Destroy(gameObject);
            }
            return true;
        }

        private void CompleteTransfer()
        {
            transform.position = transferTarget;
            Action callback = transferCallback;
            Action<PickupFlask> receiverCallback = transferReceiverCallback;
            // Clear before invoking: collection can pool and immediately reuse this instance.
            ClearTransfer();
            try
            {
                callback?.Invoke();
                receiverCallback?.Invoke(this);
            }
            finally
            {
                if (!isCollected && !IsHeld && !isTransferring && body.isKinematic) RestoreFreePhysics();
            }
        }

        private void CancelTransfer()
        {
            if (!isTransferring) return;
            ClearTransfer();
            RestoreFreePhysics();
        }

        private void ClearTransfer()
        {
            isTransferring = false;
            transferCallback = null;
            transferReceiverCallback = null;
            transferReceiver = null;
            requiresReceiver = false;
        }

        private void RestoreFreePhysics()
        {
            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = freeUseGravity;
            body.interpolation = freeInterpolation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            for (int i = 0; i < flaskColliders.Length; i++)
            {
                if (flaskColliders[i] != null) flaskColliders[i].enabled = spawnColliderStates[i];
            }
        }

        private void ApplyDefinition()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (flask == null)
            {
                freeInterpolation = body.interpolation;
                freeUseGravity = body.useGravity;
                return;
            }

            body.mass = flask.Mass;
            body.linearDamping = flask.LinearDamping;
            body.angularDamping = flask.AngularDamping;
            body.useGravity = flask.UseGravity;
            body.interpolation = flask.Interpolation;
            body.collisionDetectionMode = flask.CollisionDetection;
            freeInterpolation = flask.Interpolation;
            freeUseGravity = flask.UseGravity;
        }
    }
}
