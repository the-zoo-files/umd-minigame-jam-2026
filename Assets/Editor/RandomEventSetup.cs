using UnityEditor;
using UnityEngine;

namespace UmdJam.Editor
{
    public static class RandomEventSetup
    {
        [MenuItem("Tools/UmdJam/Configure Random Event Assets")]
        public static void Configure()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/RandomEvents"))
                AssetDatabase.CreateFolder("Assets/Resources", "RandomEvents");
            const string path = "Assets/Resources/RandomEvents/Effects.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find("UmdJam/Random Event Effects");
            if (shader == null) throw new System.InvalidOperationException("Random event effects shader is missing.");
            Material material = new(shader);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
        }
    }
}
