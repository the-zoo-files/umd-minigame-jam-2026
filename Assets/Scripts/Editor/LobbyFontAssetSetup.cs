using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace UmdJam.Editor
{
    public static class LobbyFontAssetSetup
    {
        [MenuItem("Tools/UmdJam/Configure Lobby Font Asset")]
        public static void Apply()
        {
            Configure(
                "Assets/Fonts/FredokaOne-Regular.ttf",
                "Assets/Fonts/FredokaOne-Lobby-SDF.asset",
                "FredokaOne Lobby");
            Configure(
                "Assets/Fonts/Fredoka-Regular.ttf",
                "Assets/Fonts/Fredoka-Lobby-SDF.asset",
                "Fredoka Lobby");
            AssetDatabase.SaveAssets();
            Debug.Log("Configured Lobby Fredoka font assets with 24px atlas padding.");
        }

        private static void Configure(string sourcePath, string assetPath, string assetName)
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError($"Lobby font setup could not find {sourcePath}.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            FontAsset fontAsset = FontAsset.CreateFontAsset(
                source,
                90,
                24,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            fontAsset.name = $"{assetName} SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = $"{assetName} Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            foreach (Texture2D atlas in fontAsset.atlasTextures)
            {
                if (atlas == null || AssetDatabase.Contains(atlas)) continue;
                atlas.name = $"{assetName} Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
