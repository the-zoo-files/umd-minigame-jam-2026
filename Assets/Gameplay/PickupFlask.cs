using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Action = System.Action;

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

        private void OnDisable() => activeFlasks.Remove(this);

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
            StopAllCoroutines();
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

        private void FixedUpdate()
        {
            if (IsHeld || body.linearVelocity.y >= -MaximumFallSpeed)
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
            if (isCollected || IsHeld || holdPoint == null || !isActiveAndEnabled ||
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

            transform.SetParent(holdPoint, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
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

            if (duration <= 0f)
            {
                transform.position = target;
                onArrived?.Invoke();
                return true;
            }

            StartCoroutine(MoveDirectly(target, duration, onArrived));
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

        private IEnumerator MoveDirectly(Vector3 target, float duration, Action onArrived)
        {
            Vector3 origin = transform.position;
            Quaternion originRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(origin, target, easedProgress),
                    Quaternion.AngleAxis(360f * easedProgress, Vector3.up) * originRotation);
                yield return null;
            }

            transform.position = target;
            onArrived?.Invoke();
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
