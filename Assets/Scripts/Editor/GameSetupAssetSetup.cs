using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace UmdJam.Editor
{
    public static class GameSetupAssetSetup
    {
        [MenuItem("Tools/UmdJam/Configure Game Setup Assets")]
        public static void Apply()
        {
            const string directory = "Assets/UI/Setup";
            CreateShadow(directory + "/SoftShadow.png", 188, 46f, 24f, false);
            CreateShadow(directory + "/PillShadow.png", 128, 0f, 40f, false);
            CreateShadow(directory + "/MiniShadow.png", 188, 46f, 24f, true);
            AssetDatabase.Refresh();
            const string mediumPath = "Assets/Fonts/Fredoka-Setup-Medium-SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(mediumPath) == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Fredoka-Setup-Medium.ttf");
                FontAsset medium = FontAsset.CreateFontAsset(source, 90, 24, GlyphRenderMode.SDFAA,
                    1024, 1024, AtlasPopulationMode.Dynamic, true);
                medium.name = "Fredoka Setup Medium SDF";
                AssetDatabase.CreateAsset(medium, mediumPath);
                AssetDatabase.AddObjectToAsset(medium.material, medium);
                foreach (Texture2D atlas in medium.atlasTextures)
                    if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, medium);
                EditorUtility.SetDirty(medium);
            }
            foreach (string path in Directory.GetFiles(directory, "*.png"))
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            const string inputPath = "Assets/Input/InputSystem_Actions.inputactions";
            InputActionAsset input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            InputAction attack = input != null ? input.FindAction("Player/Attack") : null;
            if (attack != null)
            {
                bool hasEast = false;
                foreach (InputBinding binding in attack.bindings)
                    if (binding.path == "<Gamepad>/buttonEast") hasEast = true;
                if (!hasEast)
                {
                    attack.AddBinding("<Gamepad>/buttonEast", groups: "Gamepad");
                    File.WriteAllText(inputPath, input.ToJson());
                    AssetDatabase.ImportAsset(inputPath);
                }
            }
        }

        private static void CreateShadow(string path, int size, float innerHalfSize, float radius, bool strongEdge)
        {
            Texture2D shadow = new(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new(Mathf.Abs(x - size * 0.5f) - innerHalfSize,
                        Mathf.Abs(y - size * 0.5f) - innerHalfSize);
                    float distance = new Vector2(Mathf.Max(offset.x, 0f), Mathf.Max(offset.y, 0f)).magnitude +
                        Mathf.Min(Mathf.Max(offset.x, offset.y), 0f) - radius;
                    float outside = Mathf.Max(0f, distance);
                    float alpha = 0.15f * Mathf.Exp(-outside * outside / 128f);
                    if (strongEdge) alpha += 0.25f * Mathf.Exp(-outside * outside / 18f);
                    pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                }
            }
            shadow.SetPixels(pixels);
            shadow.Apply();
            File.WriteAllBytes(path, shadow.EncodeToPNG());
            Object.DestroyImmediate(shadow);
        }
    }
}
