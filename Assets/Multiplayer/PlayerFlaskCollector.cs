using System.Collections.Generic;
using UmdJam.Gameplay;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerFlaskCollector : MonoBehaviour
    {
        private static readonly List<PlayerFlaskCollector> ActiveCollectors = new();

        [SerializeField, Range(1, 4)] private int playerNumber = 1;
        [SerializeField, Min(0f)] private float directThrowPadding = 2.5f;

        private Collider collectorCollider;

        public Vector3 CollectionPoint => collectorCollider.bounds.center;

        public static PlayerFlaskCollector GetForPlayer(int number)
        {
            foreach (PlayerFlaskCollector candidate in ActiveCollectors)
            {
                if (candidate != null && candidate.playerNumber == number) return candidate;
            }
            return null;
        }

        public Vector3 ApproachPoint(Vector3 from)
        {
            Bounds bounds = collectorCollider.bounds;
            bounds.Expand(Mathf.Max(0f, directThrowPadding - 0.2f) * 2f);
            Vector3 point = bounds.ClosestPoint(from);
            point.y = from.y;
            return point;
        }

        private void Awake()
        {
            collectorCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            if (!ActiveCollectors.Contains(this))
            {
                ActiveCollectors.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveCollectors.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            PickupFlask flask = other.GetComponentInParent<PickupFlask>();
            Collect(flask);
        }

        public static bool TryGetNearby(int playerNumber, Vector3 position, out PlayerFlaskCollector collector)
        {
            foreach (PlayerFlaskCollector candidate in ActiveCollectors)
            {
                if (candidate != null &&
                    candidate.playerNumber == playerNumber &&
                    candidate.ContainsWithPadding(position))
                {
                    collector = candidate;
                    return true;
                }
            }

            collector = null;
            return false;
        }

        public void Collect(PickupFlask flask)
        {
            if (flask == null || !flask.TryCollect(playerNumber, out int points))
            {
                return;
            }

            CouchPlayerController.AddScore(playerNumber, points);
        }

        private bool ContainsWithPadding(Vector3 position)
        {
            Bounds bounds = collectorCollider.bounds;
            bounds.Expand(directThrowPadding * 2f);
            return bounds.Contains(position);
        }
    }
}
