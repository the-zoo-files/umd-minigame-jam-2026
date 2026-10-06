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
        private Material collectorMaterial;
        private Color idleColor;
        private int colorProperty;
        private System.Action<PickupFlask> transferCompleted;

        public Vector3 CollectionPoint => collectorCollider.bounds.center;
        public int PlayerNumber => playerNumber;

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
            // Editor baking also reads the same destination before Awake has cached the collider.
            Collider collider = collectorCollider != null ? collectorCollider : GetComponent<Collider>();
            Bounds bounds = collider.bounds;
            bounds.Expand(Mathf.Max(0f, directThrowPadding - 0.2f) * 2f);
            Vector3 point = bounds.ClosestPoint(from);
            point.y = from.y;
            return point;
        }

        private void Awake()
        {
            transferCompleted = Collect;
            collectorCollider = GetComponent<Collider>();
            if (TryGetComponent(out Renderer collectorRenderer) && collectorRenderer.sharedMaterial != null)
            {
                Material material = collectorRenderer.sharedMaterial;
                if (material.HasProperty("_BaseColor") || material.HasProperty("_Color"))
                {
                    colorProperty = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    collectorMaterial = collectorRenderer.material;
                    idleColor = collectorMaterial.GetColor(colorProperty);
                }
            }
        }

        private void OnEnable()
        {
            CouchPlayerController.PlayerJoined += OnPlayerColorChanged;
            CouchPlayerController.ColorChanged += OnPlayerColorChanged;
            CouchPlayerController.PlayerLeft += OnPlayerLeft;
            foreach (CouchPlayerController player in CouchPlayerController.ActivePlayers) OnPlayerColorChanged(player);
            if (!ActiveCollectors.Contains(this))
            {
                ActiveCollectors.Add(this);
            }
        }

        private void OnDisable()
        {
            CouchPlayerController.PlayerJoined -= OnPlayerColorChanged;
            CouchPlayerController.ColorChanged -= OnPlayerColorChanged;
            CouchPlayerController.PlayerLeft -= OnPlayerLeft;
            ActiveCollectors.Remove(this);
        }

        private void OnDestroy()
        {
            if (collectorMaterial != null) Destroy(collectorMaterial);
        }

        private void OnPlayerColorChanged(CouchPlayerController player)
        {
            if (player.PlayerNumber == playerNumber && collectorMaterial != null)
                collectorMaterial.SetColor(colorProperty, player.PlayerColor);
        }

        private void OnPlayerLeft(CouchPlayerController player)
        {
            if (player.PlayerNumber == playerNumber && collectorMaterial != null)
                collectorMaterial.SetColor(colorProperty, idleColor);
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
            if (!isActiveAndEnabled || flask == null || !flask.TryCollect(playerNumber, out int points))
            {
                return;
            }

            CouchPlayerController.AddScore(playerNumber, points);
        }

        public bool TryTransfer(PickupFlask flask, float duration)
        {
            return isActiveAndEnabled && flask != null && flask.TryThrowDirectly(
                CollectionPoint, playerNumber, duration, this, transferCompleted);
        }

        private bool ContainsWithPadding(Vector3 position)
        {
            Bounds bounds = collectorCollider.bounds;
            bounds.Expand(directThrowPadding * 2f);
            return bounds.Contains(position);
        }
    }
}
