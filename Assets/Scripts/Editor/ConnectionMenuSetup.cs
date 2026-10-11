using UmdJam.Multiplayer;
using UmdJam.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UmdJam.Editor
{
    public static class ConnectionMenuSetup
    {
        [MenuItem("Tools/UmdJam/Configure Connection Menu")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            CouchMultiplayerManager multiplayer = Object.FindAnyObjectByType<CouchMultiplayerManager>();
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            CouchPlayerHud hud = Object.FindAnyObjectByType<CouchPlayerHud>();
            if (multiplayer == null || gameManager == null || hud == null)
            {
                throw new System.InvalidOperationException("Game scene requires its multiplayer manager, round manager, and HUD.");
            }

            SerializedObject multiplayerSettings = new(multiplayer);
            multiplayerSettings.FindProperty("gameManager").objectReferenceValue = gameManager;
            multiplayerSettings.ApplyModifiedPropertiesWithoutUndo();
            if (!hud.TryGetComponent(out CouchConnectionMenu menu))
            {
                menu = hud.gameObject.AddComponent<CouchConnectionMenu>();
            }

            SerializedObject menuSettings = new(menu);
            menuSettings.FindProperty("multiplayer").objectReferenceValue = multiplayer;
            menuSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureAndTest()
        {
            Apply();
            GameplaySmokeTests.RunConnectionMenu();
        }
    }
}
