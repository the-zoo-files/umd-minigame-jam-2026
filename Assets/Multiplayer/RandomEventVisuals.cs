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
        private readonly CouchPlayerController[] stunnedPlayers = new CouchPlayerController[4];
        private readonly Vector3[] circlePositions = new Vector3[49];
        private readonly Vector3[] stunPositions = new Vector3[17];
        private readonly Vector3[] funnelPositions = new Vector3[129];
        private readonly Vector3[] boltPositions = new Vector3[12];
        private GameObject effectRoot;
        private bool presentationActive;

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
            warning = MakeLine("Lightning warning", material, circlePositions.Length, 0.08f, new Color(1f, 0.8f, 0.1f));
            bolt = MakeLine("Lightning bolt", material, boltPositions.Length, 0.12f, new Color(0.7f, 0.85f, 1f));
            quake = MakeLine("Earthquake ripple", material, circlePositions.Length, 0.06f, new Color(1f, 0.65f, 0.15f));
            for (int i = 0; i < funnels.Length; i++)
                funnels[i] = MakeLine("Tornado funnel", material, funnelPositions.Length, 0.15f, new Color(0.75f, 0.7f, 1f));
            for (int i = 0; i < stunRings.Length; i++)
                stunRings[i] = MakeLine("Stun stars", material, stunPositions.Length, 0.07f, new Color(1f, 0.9f, 0.15f));
            strikeLight = new GameObject("Lightning flash").AddComponent<Light>();
            strikeLight.transform.SetParent(effectRoot.transform, false);
            strikeLight.type = LightType.Point;
            strikeLight.range = 12f;
            strikeLight.color = new Color(0.75f, 0.85f, 1f);
            strikeLight.shadows = LightShadows.None;
            foreach (Light light in FindObjectsByType<Light>())
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
            RandomEventKind kind = director.ActiveEvent;
            System.Array.Clear(stunnedPlayers, 0, stunnedPlayers.Length);
            bool anyStunned = false;
            var players = CouchPlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                CouchPlayerController player = players[i];
                if (player == null || !player.IsStunned || player.PlayerNumber < 1 || player.PlayerNumber > stunnedPlayers.Length) continue;
                stunnedPlayers[player.PlayerNumber - 1] = player;
                anyStunned = true;
            }
            if (kind == RandomEventKind.None && !anyStunned)
            {
                if (presentationActive) ResetPresentation();
                return;
            }
            presentationActive = true;
            RestoreCamera();
            bool earthquake = kind == RandomEventKind.Earthquake;
            quake.enabled = earthquake;
            if (earthquake)
            {
                Bounds bounds = GameManager.Instance.ArenaBounds;
                Circle(quake, circlePositions, new Vector3(bounds.center.x, bounds.min.y + 0.09f, bounds.center.z),
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
                for (int p = 0; p < funnelPositions.Length; p++)
                {
                    float fraction = (float)p / (funnelPositions.Length - 1);
                    float angle = fraction * Mathf.PI * 10f + Time.time * 8f + i;
                    float radius = Mathf.Lerp(0.15f, director.TornadoRadius, fraction);
                    funnelPositions[p] = center + new Vector3(Mathf.Cos(angle) * radius,
                        fraction * director.TornadoHeight, Mathf.Sin(angle) * radius);
                }
                funnels[i].SetPositions(funnelPositions);
            }
            float flash = director.StrikeFlash;
            warning.enabled = kind == RandomEventKind.Lightning && director.IsWarning && flash <= 0f;
            if (warning.enabled)
                Circle(warning, circlePositions, director.LightningPosition, director.LightningRadius, Time.time * 2f);
            bolt.enabled = kind == RandomEventKind.Lightning && flash > 0f;
            if (bolt.enabled)
            {
                for (int p = 0; p < boltPositions.Length; p++)
                {
                    float fraction = (float)p / (boltPositions.Length - 1);
                    float offset = p == boltPositions.Length - 1 ? 0f : Mathf.Sin(p * 13.7f + Time.time * 50f) * 0.5f;
                    boltPositions[p] = director.LightningPosition +
                        new Vector3(offset, (1f - fraction) * 12f, offset * 0.4f);
                }
                bolt.SetPositions(boltPositions);
            }
            strikeLight.transform.position = director.LightningPosition + Vector3.up * 2f;
            strikeLight.intensity = flash * 14f;
            strikeLight.enabled = flash > 0f;
            if (sceneLight != null) sceneLight.intensity = originalIntensity * (1f + flash * 2f);
            for (int slot = 0; slot < stunRings.Length; slot++)
            {
                CouchPlayerController player = stunnedPlayers[slot];
                stunRings[slot].enabled = player != null;
                if (stunRings[slot].enabled)
                    Circle(stunRings[slot], stunPositions, player.transform.position + Vector3.up * 1.4f, 0.45f, Time.time * 4f);
            }
        }

        private static void Circle(LineRenderer line, Vector3[] positions, Vector3 center, float radius, float phase)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                float angle = (float)i / (positions.Length - 1) * Mathf.PI * 2f + phase;
                positions[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            }
            line.SetPositions(positions);
        }

        private void RestoreCamera()
        {
            if (arenaCamera != null) arenaCamera.transform.position -= cameraOffset;
            cameraOffset = Vector3.zero;
        }

        public void ResetPresentation()
        {
            presentationActive = false;
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
