using UnityEngine;

namespace UmdJam.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class PickupFlask : MonoBehaviour
    {
        private Rigidbody body;
        private Collider[] flaskColliders;
        private Transform currentHoldPoint;
        private RigidbodyInterpolation freeInterpolation;

        [SerializeField] private float maximumFallSpeed = 14f;
        [SerializeField] private float impactSpinBoost = 2.5f;
        [SerializeField] private float firstBounceSpeed = 1.5f;
        [SerializeField] private int maximumBounces = 1;

        private int bounceCount;

        public bool IsHeld { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            flaskColliders = GetComponentsInChildren<Collider>();
            freeInterpolation = body.interpolation;
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
            if (IsHeld || body.linearVelocity.y >= -maximumFallSpeed)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = -maximumFallSpeed;
            body.linearVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsHeld || collision.relativeVelocity.sqrMagnitude < 9f)
            {
                return;
            }

            body.AddTorque(Random.onUnitSphere * impactSpinBoost, ForceMode.VelocityChange);

            if (bounceCount >= maximumBounces || collision.contactCount == 0 || collision.GetContact(0).normal.y < 0.55f)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.y = firstBounceSpeed * Mathf.Pow(0.55f, bounceCount);
            body.linearVelocity = velocity;
            bounceCount++;
        }

        public bool TryPickUp(Transform holdPoint)
        {
            if (IsHeld || holdPoint == null || !isActiveAndEnabled ||
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

        public bool TryThrow(Vector3 launchVelocity)
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
            body.useGravity = true;
            body.interpolation = freeInterpolation;
            body.linearVelocity = launchVelocity;
            body.angularVelocity = Random.onUnitSphere * 8f;
            IsHeld = false;
            return true;
        }
    }
}
