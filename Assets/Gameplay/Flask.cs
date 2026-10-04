using UnityEngine;

namespace UmdJam.Gameplay
{
    [CreateAssetMenu(fileName = "Flask", menuName = "Umd Jam/Flask")]
    public sealed class Flask : ScriptableObject
    {
        [SerializeField] private PickupFlask prefab;
        [SerializeField, Min(0)] private int points = 1;

        [Header("Physics")]
        [SerializeField, Min(0.01f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float linearDamping;
        [SerializeField, Min(0f)] private float angularDamping = 0.05f;
        [SerializeField] private bool useGravity = true;
        [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.Continuous;
        [SerializeField, Min(0f)] private float maximumFallSpeed = 14f;
        [SerializeField, Min(0f)] private float thrownSpinSpeed = 8f;
        [SerializeField, Min(0f)] private float impactSpinBoost = 2.5f;
        [SerializeField, Min(0f)] private float firstBounceSpeed = 1.5f;
        [SerializeField, Min(0)] private int maximumBounces = 1;

        public PickupFlask Prefab => prefab;
        public int Points => points;
        public float Mass => mass;
        public float LinearDamping => linearDamping;
        public float AngularDamping => angularDamping;
        public bool UseGravity => useGravity;
        public RigidbodyInterpolation Interpolation => interpolation;
        public CollisionDetectionMode CollisionDetection => collisionDetection;
        public float MaximumFallSpeed => maximumFallSpeed;
        public float ThrownSpinSpeed => thrownSpinSpeed;
        public float ImpactSpinBoost => impactSpinBoost;
        public float FirstBounceSpeed => firstBounceSpeed;
        public int MaximumBounces => maximumBounces;
    }
}
