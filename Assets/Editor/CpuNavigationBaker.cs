using UmdJam.Multiplayer;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace UmdJam.Editor
{
    public sealed class CpuNavigationBaker : IPreprocessBuildWithReport
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string AssetPath = "Assets/Resources/CpuNavigation/Game.asset";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Bake();

        [MenuItem("Tools/UmdJam/Bake CPU Navigation")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("CPU navigation must be baked outside Play Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new BuildFailedException("Save Game.unity before baking CPU navigation.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            NavMeshData data = null;
            try
            {
                GameManager game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    game = root.GetComponentInChildren<GameManager>();
                    if (game != null) break;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Multiplayer/Player.prefab");
                CharacterController character = prefab != null ? prefab.GetComponent<CharacterController>() : null;
                if (game == null || character == null)
                    throw new BuildFailedException("CPU navigation requires the GameManager and player capsule.");
                Bounds bounds = game.ArenaBounds;
                data = CpuNavigation.Bake(bounds, character, scene);
                if (data == null) throw new BuildFailedException("CPU navigation bake failed.");
                ValidateRoutes(data, character, scene);

                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/CpuNavigation"))
                    AssetDatabase.CreateFolder("Assets/Resources", "CpuNavigation");
                CpuNavigationBake asset = AssetDatabase.LoadAssetAtPath<CpuNavigationBake>(AssetPath);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CpuNavigationBake>();
                    AssetDatabase.CreateAsset(asset, AssetPath);
                }
                NavMeshData previous = asset.Data;
                data.name = "Game CPU navigation";
                AssetDatabase.AddObjectToAsset(data, asset);
                asset.Initialize(data, ScenePath, bounds, CpuNavigation.CreateBuildSettings(character),
                    AssetDatabase.GetAssetDependencyHash(ScenePath).ToString());
                if (previous != null) Object.DestroyImmediate(previous, true);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                data = null; // AssetDatabase owns the successful bake.
                Debug.Log("CPU navigation baked successfully.");
            }
            finally
            {
                if (data != null) Object.DestroyImmediate(data);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateRoutes(NavMeshData data, CharacterController character, Scene scene)
        {
            NavMeshDataInstance registered = NavMesh.AddNavMeshData(data);
            if (!registered.valid) throw new BuildFailedException("CPU navigation bake could not be registered.");
            try
            {
                NavMeshQueryFilter filter = new()
                {
                    agentTypeID = CpuNavigation.CreateBuildSettings(character).agentTypeID,
                    areaMask = NavMesh.AllAreas
                };
                NavMeshPath path = new();
                PlayerFlaskCollector[] collectors = new PlayerFlaskCollector[CouchMultiplayerManager.MaximumPlayers];
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (PlayerFlaskCollector collector in root.GetComponentsInChildren<PlayerFlaskCollector>())
                    {
                        int slot = collector.PlayerNumber - 1;
                        if (slot >= 0 && slot < collectors.Length) collectors[slot] = collector;
                    }
                }
                for (int slot = 0; slot < collectors.Length; slot++)
                {
                    Vector3 spawn = CouchPlayerController.GetSpawnPosition(slot);
                    if (collectors[slot] == null)
                        throw new BuildFailedException($"CPU navigation requires collector {slot + 1}.");
                    Vector3 destination = collectors[slot].ApproachPoint(spawn);
                    if (!NavMesh.SamplePosition(spawn, out NavMeshHit start, 2f, filter) ||
                        !NavMesh.SamplePosition(destination, out NavMeshHit end, 2f, filter) ||
                        CpuNavigation.PlanarDistance(destination, end.position) > 0.8f ||
                        !NavMesh.CalculatePath(start.position, end.position, filter, path) ||
                        path.status != NavMeshPathStatus.PathComplete)
                        throw new BuildFailedException($"CPU navigation cannot reach collector {slot + 1} from its spawn.");
                }
            }
            finally { registered.Remove(); }
        }
    }
}
