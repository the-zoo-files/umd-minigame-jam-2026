using System.Collections.Generic;
using UmdJam.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace UmdJam.Multiplayer
{
    // A shared, scene-owned map. Bots follow paths through the normal CharacterController.
    public sealed class CpuNavigation : MonoBehaviour
    {
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavMeshQueryFilter filter;
        private bool ownsData;
        private readonly DistanceEntry[] distanceCache = new DistanceEntry[64];
        private int distanceCacheCount;
        private int nextDistanceEntry;

        private struct DistanceEntry
        {
            public Vector3 From;
            public Vector3 Destination;
            public bool Reachable;
            public float Distance;
            public int CornerCapacity;
        }

        public bool IsReady => instance.valid;
        public bool UsesBakedData { get; private set; }

        public bool Build(Bounds bounds, CharacterController character)
        {
            if (IsReady) return true;
            Release();
            NavMeshBuildSettings settings = CreateBuildSettings(character);
            CpuNavigationBake bake = Resources.Load<CpuNavigationBake>("CpuNavigation/" + gameObject.scene.name);
            if (bake != null && bake.Matches(gameObject.scene.path, bounds, settings))
            {
                data = bake.Data;
                UsesBakedData = true;
            }
            else
            {
                data = Bake(bounds, character, gameObject.scene);
                ownsData = true;
            }
            if (data == null) return false;
            instance = NavMesh.AddNavMeshData(data);
            filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            if (!IsReady) Release();
            return IsReady;
        }

        public static NavMeshData Bake(Bounds bounds, CharacterController character, UnityEngine.SceneManagement.Scene scene)
        {
            List<NavMeshBuildSource> sources = new();
            List<NavMeshBuildMarkup> markups = new();
            List<NavMeshBuildSource> rootSources = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                    false, markups, false, rootSources);
                sources.AddRange(rootSources);
            }
            sources.RemoveAll(source => source.component is Collider collider &&
                (collider.isTrigger || collider.GetComponentInParent<CouchPlayerController>() != null ||
                 collider.GetComponentInParent<PickupFlask>() != null));

            return NavMeshBuilder.BuildNavMeshData(CreateBuildSettings(character), sources, bounds,
                Vector3.zero, Quaternion.identity);
        }

        public static NavMeshBuildSettings CreateBuildSettings(CharacterController character)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = character.radius + character.skinWidth;
            settings.agentHeight = character.height;
            settings.agentClimb = character.stepOffset;
            settings.agentSlope = character.slopeLimit;
            settings.overrideVoxelSize = true;
            settings.voxelSize = Mathf.Max(0.05f, character.radius / 4f);
            return settings;
        }

        public bool TryDistance(Vector3 from, Vector3 destination, NavMeshPath path, Vector3[] corners,
            out float distance)
        {
            distance = 0f;
            if (!IsReady || path == null || corners == null || corners.Length < 2 ||
                !Ballistics.IsFinite(from) || !Ballistics.IsFinite(destination)) return false;
            destination.y = from.y;
            for (int i = 0; i < distanceCacheCount; i++)
            {
                DistanceEntry cached = distanceCache[i];
                // Exact endpoints only: never approximate a moving flask's route cost.
                if (!cached.From.Equals(from) || !cached.Destination.Equals(destination) ||
                    cached.CornerCapacity != corners.Length) continue;
                distance = cached.Distance;
                return cached.Reachable;
            }

            bool reachable = TryPath(from, destination, path, corners, out _, out distance);
            distanceCache[nextDistanceEntry] = new DistanceEntry
            {
                From = from, Destination = destination, Reachable = reachable, Distance = distance,
                CornerCapacity = corners.Length
            };
            nextDistanceEntry = (nextDistanceEntry + 1) % distanceCache.Length;
            distanceCacheCount = Mathf.Min(distanceCacheCount + 1, distanceCache.Length);
            return reachable;
        }

        public bool TryPath(Vector3 from, Vector3 destination, NavMeshPath path, Vector3[] corners,
            out int count, out float distance)
        {
            count = 0;
            distance = 0f;
            if (path == null || corners == null || corners.Length < 2 ||
                !Ballistics.IsFinite(from) || !Ballistics.IsFinite(destination)) return false;
            // Query near the player's floor, not the airborne flask or machine roof.
            destination.y = from.y;
            if (!IsReady || !NavMesh.SamplePosition(from, out NavMeshHit start, 2f, filter) ||
                !NavMesh.SamplePosition(destination, out NavMeshHit end, 2f, filter) ||
                PlanarDistance(destination, end.position) > 0.8f ||
                !NavMesh.CalculatePath(start.position, end.position, filter, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            count = path.GetCornersNonAlloc(corners);
            if (count == 0 || count == corners.Length) return false;
            Vector3 previous = from;
            for (int i = 0; i < count; i++)
            {
                distance += PlanarDistance(previous, corners[i]);
                previous = corners[i];
            }
            return true;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            if (instance.valid) instance.Remove();
            if (ownsData && data != null) Destroy(data);
            data = null;
            ownsData = false;
            UsesBakedData = false;
            distanceCacheCount = 0;
            nextDistanceEntry = 0;
        }
    }
}
