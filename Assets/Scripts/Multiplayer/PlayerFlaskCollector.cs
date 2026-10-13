using System.Collections.Generic;
using UmdJam.Gameplay;
using UnityEngine;

namespace UmdJam.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerFlaskCollector : MonoBehaviour
    {
        public static event System.Action<PlayerFlaskCollector, int> FlaskCollected;
        private static readonly List<PlayerFlaskCollector> ActiveCollectors = new();

        [SerializeField, Range(1, 4)] private int playerNumber = 1;
        [SerializeField, Min(0f)] private float directThrowPadding = 2.5f;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField, Min(0)] private int indicatorMaterialIndex;

        private Collider collectorCollider;
        private Material collectorMaterial;
        private Color idleColor;
        private int colorProperty;
        private System.Action<PickupFlask> transferCompleted;
        private Material indicatorMaterial;
        private Material originalIndicatorMaterial;
        private float indicatorEmissionStrength;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public Vector3 CollectionPoint => collectorCollider.bounds.center;
        public int PlayerNumber => playerNumber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => FlaskCollected = null;

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
            if (indicatorRenderer != null)
            {
                Material[] materials = indicatorRenderer.sharedMaterials;
                if (indicatorMaterialIndex >= 0 && indicatorMaterialIndex < materials.Length &&
                    materials[indicatorMaterialIndex] != null)
                {
                    originalIndicatorMaterial = materials[indicatorMaterialIndex];
                    indicatorMaterial = new Material(originalIndicatorMaterial);
                    indicatorEmissionStrength = indicatorMaterial.HasProperty(EmissionColor)
                        ? indicatorMaterial.GetColor(EmissionColor).maxColorComponent : 0f;
                    materials[indicatorMaterialIndex] = indicatorMaterial;
                    indicatorRenderer.sharedMaterials = materials;
                }
            }
        }

        private void OnEnable()
        {
            ResetIndicatorColor();
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
            if (indicatorMaterial != null)
            {
                if (indicatorRenderer != null)
                {
                    Material[] materials = indicatorRenderer.sharedMaterials;
                    if (indicatorMaterialIndex >= 0 && indicatorMaterialIndex < materials.Length &&
                        materials[indicatorMaterialIndex] == indicatorMaterial)
                    {
                        materials[indicatorMaterialIndex] = originalIndicatorMaterial;
                        indicatorRenderer.sharedMaterials = materials;
                    }
                }
                Destroy(indicatorMaterial);
            }
        }

        private void OnPlayerColorChanged(CouchPlayerController player)
        {
            if (player.PlayerNumber != playerNumber) return;
            if (collectorMaterial != null) collectorMaterial.SetColor(colorProperty, player.PlayerColor);
            if (indicatorMaterial == null) return;
            if (indicatorMaterial.HasProperty(BaseColor)) indicatorMaterial.SetColor(BaseColor, player.PlayerColor);
            if (indicatorMaterial.HasProperty(LegacyColor)) indicatorMaterial.SetColor(LegacyColor, player.PlayerColor);
            if (indicatorMaterial.HasProperty(EmissionColor))
                indicatorMaterial.SetColor(EmissionColor, player.PlayerColor * indicatorEmissionStrength);
        }

        private void OnPlayerLeft(CouchPlayerController player)
        {
            if (player.PlayerNumber != playerNumber) return;
            if (collectorMaterial != null) collectorMaterial.SetColor(colorProperty, idleColor);
            ResetIndicatorColor();
        }

        private void ResetIndicatorColor()
        {
            if (indicatorMaterial == null || originalIndicatorMaterial == null) return;
            if (indicatorMaterial.HasProperty(BaseColor))
                indicatorMaterial.SetColor(BaseColor, originalIndicatorMaterial.GetColor(BaseColor));
            if (indicatorMaterial.HasProperty(LegacyColor))
                indicatorMaterial.SetColor(LegacyColor, originalIndicatorMaterial.GetColor(LegacyColor));
            if (indicatorMaterial.HasProperty(EmissionColor))
                indicatorMaterial.SetColor(EmissionColor, originalIndicatorMaterial.GetColor(EmissionColor));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
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
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
            if (!isActiveAndEnabled || flask == null || !flask.TryCollect(playerNumber, out int points))
            {
                return;
            }

            CouchPlayerController.AddScore(playerNumber, points);
            if (points > 0) FlaskCollected?.Invoke(this, points);
        }

        public bool TryTransfer(PickupFlask flask, float duration)
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return false;
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
