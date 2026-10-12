using UnityEditor;
using UnityEngine;

namespace UmdJam.Editor
{
    public static class GameAudioAssetSetup
    {
        [MenuItem("Tools/UmdJam/Configure Game Audio Imports")]
        public static void Apply()
        {
            foreach (string name in new[] { "ButtonClick", "Ready", "Point" })
            {
                string path = $"Assets/Sounds/Resources/Audio/{name}.wav";
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new System.InvalidOperationException($"Missing prepared game sound: {path}");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.forceToMono = false;
                importer.SaveAndReimport();
            }
            Debug.Log("Game sound imports configured for preloaded PCM playback.");
        }
    }
}
