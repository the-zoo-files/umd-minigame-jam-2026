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

        public bool IsReady => instance.valid;

        public bool Build(Bounds bounds, CharacterController character)
        {
            if (IsReady) return true;
            List<NavMeshBuildSource> sources = new();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                false, new List<NavMeshBuildMarkup>(), false, sources);
            sources.RemoveAll(source => source.component is Collider collider &&
                (collider.isTrigger || collider.GetComponentInParent<CouchPlayerController>() != null ||
                 collider.GetComponentInParent<PickupFlask>() != null));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = character.radius + character.skinWidth;
            settings.agentHeight = character.height;
            settings.agentClimb = character.stepOffset;
            settings.agentSlope = character.slopeLimit;
            settings.overrideVoxelSize = true;
            settings.voxelSize = Mathf.Max(0.05f, character.radius / 4f);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return false;
            instance = NavMesh.AddNavMeshData(data);
            filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            return IsReady;
        }

        public bool TryPath(Vector3 from, Vector3 destination, NavMeshPath path, Vector3[] corners,
            out int count, out float distance)
        {
            count = 0;
            distance = 0f;
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

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data != null) Destroy(data);
        }
    }
}
