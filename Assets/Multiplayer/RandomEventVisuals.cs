using UnityEngine;

namespace UmdJam.Multiplayer
{
    // Reusable procedural effects: no emitters, physics colliders, or per-frame allocations.
    public sealed class RandomEventVisuals : MonoBehaviour
    {
        private RandomEventDirector director;
        private Camera arenaCamera;
        private Vector3 cameraOffset;
        private Light sceneLight;
        private float originalIntensity;
        private Light strikeLight;
        private LineRenderer warning;
        private LineRenderer bolt;
        private LineRenderer quake;
        private readonly LineRenderer[] funnels = new LineRenderer[2];
        private readonly LineRenderer[] stunRings = new LineRenderer[4];
        private GameObject effectRoot;

        public void Configure(RandomEventDirector owner)
        {
            director = owner;
            arenaCamera = Camera.main;
            Material material = Resources.Load<Material>("RandomEvents/Effects");
            if (material == null)
            {
                Debug.LogError("Random events need Resources/RandomEvents/Effects.mat. Run Configure Random Event Assets.", this);
                enabled = false;
                return;
            }
            effectRoot = new GameObject("Random event visuals");
            effectRoot.transform.SetParent(transform, false);
            warning = MakeLine("Lightning warning", material, 49, 0.08f, new Color(1f, 0.8f, 0.1f));
            bolt = MakeLine("Lightning bolt", material, 12, 0.12f, new Color(0.7f, 0.85f, 1f));
            quake = MakeLine("Earthquake ripple", material, 49, 0.06f, new Color(1f, 0.65f, 0.15f));
            for (int i = 0; i < funnels.Length; i++)
                funnels[i] = MakeLine("Tornado funnel", material, 129, 0.15f, new Color(0.75f, 0.7f, 1f));
            for (int i = 0; i < stunRings.Length; i++)
                stunRings[i] = MakeLine("Stun stars", material, 17, 0.07f, new Color(1f, 0.9f, 0.15f));
            strikeLight = new GameObject("Lightning flash").AddComponent<Light>();
            strikeLight.transform.SetParent(effectRoot.transform, false);
            strikeLight.type = LightType.Point;
            strikeLight.range = 12f;
            strikeLight.color = new Color(0.75f, 0.85f, 1f);
            strikeLight.shadows = LightShadows.None;
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light != strikeLight && light.isActiveAndEnabled && light.type == LightType.Directional)
                {
                    sceneLight = light;
                    originalIntensity = light.intensity;
                    break;
                }
            }
            ResetPresentation();
        }

        private LineRenderer MakeLine(string name, Material material, int count, float width, Color color)
        {
            LineRenderer line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(effectRoot.transform, false);
            line.sharedMaterial = material;
            line.positionCount = count;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void LateUpdate()
        {
            if (director == null || effectRoot == null) return;
            RestoreCamera();
            RandomEventKind kind = director.ActiveEvent;
            bool earthquake = kind == RandomEventKind.Earthquake;
            quake.enabled = earthquake;
            if (earthquake)
            {
                Bounds bounds = GameManager.Instance.ArenaBounds;
                Circle(quake, new Vector3(bounds.center.x, bounds.min.y + 0.09f, bounds.center.z),
                    1f + Mathf.Repeat(director.Elapsed * 3f, 7f), 0f);
                if (arenaCamera != null)
                {
                    float phase = Time.time * 33f;
                    cameraOffset = (arenaCamera.transform.right * Mathf.Sin(phase) +
                        arenaCamera.transform.up * Mathf.Sin(phase * 1.37f)) * 0.09f;
                    arenaCamera.transform.position += cameraOffset;
                }
            }
            for (int i = 0; i < funnels.Length; i++)
            {
                funnels[i].enabled = kind == RandomEventKind.Tornadoes;
                if (!funnels[i].enabled) continue;
                Vector3 center = director.TornadoPosition(i);
                for (int p = 0; p < funnels[i].positionCount; p++)
                {
                    float fraction = (float)p / (funnels[i].positionCount - 1);
                    float angle = fraction * Mathf.PI * 10f + Time.time * 8f + i;
                    float radius = Mathf.Lerp(0.15f, director.TornadoRadius, fraction);
                    funnels[i].SetPosition(p, center + new Vector3(Mathf.Cos(angle) * radius,
                        fraction * director.TornadoHeight, Mathf.Sin(angle) * radius));
                }
            }
            float flash = director.StrikeFlash;
            warning.enabled = kind == RandomEventKind.Lightning && director.IsWarning && flash <= 0f;
            if (warning.enabled)
                Circle(warning, director.LightningPosition, director.LightningRadius, Time.time * 2f);
            bolt.enabled = kind == RandomEventKind.Lightning && flash > 0f;
            if (bolt.enabled)
            {
                for (int p = 0; p < bolt.positionCount; p++)
                {
                    float fraction = (float)p / (bolt.positionCount - 1);
                    float offset = p == bolt.positionCount - 1 ? 0f : Mathf.Sin(p * 13.7f + Time.time * 50f) * 0.5f;
                    bolt.SetPosition(p, director.LightningPosition +
                        new Vector3(offset, (1f - fraction) * 12f, offset * 0.4f));
                }
            }
            strikeLight.transform.position = director.LightningPosition + Vector3.up * 2f;
            strikeLight.intensity = flash * 14f;
            strikeLight.enabled = flash > 0f;
            if (sceneLight != null) sceneLight.intensity = originalIntensity * (1f + flash * 2f);
            for (int slot = 0; slot < stunRings.Length; slot++)
            {
                CouchPlayerController player = null;
                var players = CouchPlayerController.ActivePlayers;
                for (int i = 0; i < players.Count; i++)
                {
                    CouchPlayerController candidate = players[i];
                    if (candidate != null && candidate.PlayerNumber == slot + 1) player = candidate;
                }
                stunRings[slot].enabled = player != null && player.IsStunned;
                if (stunRings[slot].enabled)
                    Circle(stunRings[slot], player.transform.position + Vector3.up * 1.4f, 0.45f, Time.time * 4f);
            }
        }

        private static void Circle(LineRenderer line, Vector3 center, float radius, float phase)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = (float)i / (line.positionCount - 1) * Mathf.PI * 2f + phase;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        private void RestoreCamera()
        {
            if (arenaCamera != null) arenaCamera.transform.position -= cameraOffset;
            cameraOffset = Vector3.zero;
        }

        public void ResetPresentation()
        {
            RestoreCamera();
            if (sceneLight != null) sceneLight.intensity = originalIntensity;
            if (effectRoot == null) return;
            if (warning != null) warning.enabled = false;
            if (bolt != null) bolt.enabled = false;
            if (quake != null) quake.enabled = false;
            foreach (LineRenderer funnel in funnels) if (funnel != null) funnel.enabled = false;
            foreach (LineRenderer ring in stunRings) if (ring != null) ring.enabled = false;
            if (strikeLight != null) strikeLight.enabled = false;
        }

        private void OnDisable() => ResetPresentation();
        private void OnDestroy()
        {
            ResetPresentation();
            if (effectRoot != null) Destroy(effectRoot);
        }
    }
}
