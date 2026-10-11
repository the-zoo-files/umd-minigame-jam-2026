using UnityEngine;
using UnityEngine.AI;

namespace UmdJam.Multiplayer
{
    // Generated through Editor APIs. Runtime registration never owns/destroys this shared asset.
    public sealed class CpuNavigationBake : ScriptableObject
    {
        [SerializeField] private NavMeshData data;
        [SerializeField] private string scenePath;
        [SerializeField] private Bounds bounds;
        [SerializeField] private BuildConfiguration settings;
        [SerializeField] private string sourceHash;

        public NavMeshData Data => data;

        public void Initialize(NavMeshData navigation, string scene, Bounds arena,
            NavMeshBuildSettings configuration, string hash)
        {
            data = navigation;
            scenePath = scene;
            bounds = arena;
            settings = new BuildConfiguration(configuration);
            sourceHash = hash;
        }

        public bool Matches(string scene, Bounds arena, NavMeshBuildSettings configuration)
        {
            if (data == null || scene != scenePath || !bounds.Equals(arena) ||
                !settings.Equals(new BuildConfiguration(configuration))) return false;
#if UNITY_EDITOR
            // Authored geometry edits must not silently use yesterday's bake in Play Mode.
            if (sourceHash != UnityEditor.AssetDatabase.GetAssetDependencyHash(scene).ToString()) return false;
#endif
            return true;
        }

        // NavMeshBuildSettings is a native API struct, not a Unity-serializable asset field.
        [System.Serializable]
        private struct BuildConfiguration
        {
            [SerializeField] private int agentType;
            [SerializeField] private float radius;
            [SerializeField] private float height;
            [SerializeField] private float climb;
            [SerializeField] private float slope;
            [SerializeField] private float regionArea;
            [SerializeField] private bool overrideVoxels;
            [SerializeField] private float voxelSize;
            [SerializeField] private bool overrideTiles;
            [SerializeField] private int tileSize;
            [SerializeField] private bool heightMesh;

            public BuildConfiguration(NavMeshBuildSettings source)
            {
                agentType = source.agentTypeID;
                radius = source.agentRadius;
                height = source.agentHeight;
                climb = source.agentClimb;
                slope = source.agentSlope;
                regionArea = source.minRegionArea;
                overrideVoxels = source.overrideVoxelSize;
                voxelSize = source.voxelSize;
                overrideTiles = source.overrideTileSize;
                tileSize = source.tileSize;
                heightMesh = source.buildHeightMesh;
            }
        }
    }
}
