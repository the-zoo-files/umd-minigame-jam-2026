using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BakeAdaptiveProbes
{
    private const string BakingSetPath = "Assets/Scenes/Game/Game Baking Set.asset";

    public static object Start()
    {
        if (AdaptiveProbeVolumes.isRunning)
        {
            return new { started = false, isRunning = true, message = "An adaptive probe bake is already running." };
        }

        bool started = AdaptiveProbeVolumes.BakeAsync();
        return new { started, isRunning = AdaptiveProbeVolumes.isRunning };
    }

    public static object Status()
    {
        return new
        {
            isRunning = AdaptiveProbeVolumes.isRunning
        };
    }

    public static object Verify()
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        UnityEngine.Object bakingSet = AssetDatabase.LoadMainAssetAtPath(BakingSetPath);
        if (bakingSet == null)
        {
            throw new InvalidOperationException($"Missing baking set at {BakingSetPath}.");
        }

        SerializedObject serializedSet = new SerializedObject(bakingSet);
        SerializedProperty scenarios = serializedSet.FindProperty("m_LightingScenarios");
        SerializedProperty cellDescs = serializedSet.FindProperty("cellDescs");
        SerializedProperty globalBounds = serializedSet.FindProperty("globalBounds");

        return new
        {
            isRunning = AdaptiveProbeVolumes.isRunning,
            bakingSet = BakingSetPath,
            lightingScenarioCount = scenarios != null && scenarios.isArray ? scenarios.arraySize : -1,
            cellDescriptorCount = cellDescs != null && cellDescs.isArray ? cellDescs.arraySize : -1,
            hasGlobalBounds = globalBounds != null
        };
    }
}
