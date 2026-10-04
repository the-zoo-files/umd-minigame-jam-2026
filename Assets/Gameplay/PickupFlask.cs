using System.Collections;
using UnityEngine;
using Action = System.Action;

namespace UmdJam.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PickupFlask : MonoBehaviour
    {
        [SerializeField] private Flask flask;

        private Rigidbody body;
        private Collider[] flaskColliders;
        private Transform currentHoldPoint;
        private RigidbodyInterpolation freeInterpolation;
        private bool freeUseGravity;
        private int bounceCount;
        private int lastThrowerPlayerNumber;
        private bool isCollected;

        private float MaximumFallSpeed => flask != null ? flask.MaximumFallSpeed : 14f;
        private float ImpactSpinBoost => flask != null ? flask.ImpactSpinBoost : 2.5f;
        private float FirstBounceSpeed => flask != null ? flask.FirstBounceSpeed : 1.5f;
        private int MaximumBounces => flask != null ? flask.MaximumBounces : 1;
        private float ThrownSpinSpeed => flask != null ? flask.ThrownSpinSpeed : 8f;

        public bool IsHeld { get; private set; }
        public bool IsCollected => isCollected;
        public int LastThrowerPlayerNumber => lastThrowerPlayerNumber;
        public int PointValue => flask != null ? flask.Points : 1;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            flaskColliders = GetComponentsInChildren<Collider>();
            ApplyDefinition();
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

        public void PickUp(Transform holdPoint)
        {
            if (isCollected)
            {
                return;
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
        }

        public void Throw(Vector3 launchVelocity, int playerNumber)
        {
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
        }

        public void ThrowDirectly(Vector3 target, int playerNumber, float duration, Action onArrived)
        {
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
                return;
            }

            StartCoroutine(MoveDirectly(target, duration, onArrived));
        }

        public void Initialize(Flask definition)
        {
            flask = definition;
            ApplyDefinition();
        }

        public bool TryCollect(int playerNumber, out int points)
        {
            points = 0;
            if (isCollected || IsHeld || playerNumber <= 0 || lastThrowerPlayerNumber != playerNumber)
            {
                return false;
            }

            isCollected = true;
            points = PointValue;
            Destroy(gameObject);
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
