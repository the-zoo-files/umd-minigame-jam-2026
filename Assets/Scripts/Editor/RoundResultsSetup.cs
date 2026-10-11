using System;
using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UmdJam.Editor
{
    public static class RoundResultsSetup
    {
        [MenuItem("Tools/UmdJam/Configure Round Results")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Game.unity")
                throw new InvalidOperationException("Open Game.unity before configuring round results.");
            GameManager manager = Object.FindAnyObjectByType<GameManager>();
            CouchPlayerHud hud = Object.FindAnyObjectByType<CouchPlayerHud>();
            Camera camera = Camera.main;
            if (manager == null || hud == null || camera == null)
                throw new InvalidOperationException("Results requires the scene's GameManager, HUD, and main camera.");
            if (!hud.TryGetComponent(out RoundResultsView view)) view = Undo.AddComponent<RoundResultsView>(hud.gameObject);
            GameObject root = GameObject.Find("RoundResults");
            if (root == null)
            {
                root = new GameObject("RoundResults");
                Undo.RegisterCreatedObjectUndo(root, "Create round results");
            }
            if (!root.TryGetComponent(out RoundResultsDirector director))
                director = Undo.AddComponent<RoundResultsDirector>(root);
            Transform target = root.transform.Find("ResultsCameraTarget");
            if (target == null)
            {
                GameObject targetObject = new("ResultsCameraTarget");
                Undo.RegisterCreatedObjectUndo(targetObject, "Create results camera target");
                target = targetObject.transform;
                target.SetParent(root.transform);
                Bounds bounds = manager.ArenaBounds;
                target.SetPositionAndRotation(new Vector3(bounds.center.x, 18f, bounds.center.z),
                    Quaternion.Euler(90f, 0f, 0f));
            }
            SetReference(view, "arenaCamera", camera);
            SetReference(director, "gameManager", manager);
            SetReference(director, "arenaCamera", camera);
            SetReference(director, "resultsCameraTarget", target);
            SetReference(director, "view", view);
            SetReference(director, "hud", hud);
            // Hide only fixture geometry during the overhead view; baked lights remain untouched.
            GameObject fixtures = GameObject.Find("CeilingLights");
            if (fixtures != null)
            {
                SerializedObject settings = new(director);
                SerializedProperty occluders = settings.FindProperty("overheadOccluders");
                if (occluders.arraySize == 0)
                {
                    Renderer[] renderers = fixtures.GetComponentsInChildren<Renderer>();
                    occluders.arraySize = renderers.Length;
                    for (int i = 0; i < renderers.Length; i++)
                        occluders.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                    settings.ApplyModifiedProperties();
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void SetReference(Object target, string property, Object value)
        {
            Undo.RecordObject(target, "Wire round results");
            SerializedObject settings = new(target);
            settings.FindProperty(property).objectReferenceValue = value;
            settings.ApplyModifiedProperties();
        }
    }
}
