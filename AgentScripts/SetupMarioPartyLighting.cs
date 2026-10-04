using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class SetupMarioPartyLighting
{
    private const string LightingRootName = "CeilingLights";
    private const string MaterialFolder = "Assets/Materials/CeilingLights";
    private const string LightingSettingsPath = "Assets/Scenes/Game/MarioPartyLightingSettings.lighting";
    private const string BakingSetPath = "Assets/Scenes/Game/Game Baking Set.asset";
    private static Mesh s_QuadMesh;

    public static object Main()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            throw new InvalidOperationException("A loaded scene is required.");
        }

        EnsureFolder(MaterialFolder);

        Transform lightingRoot = FindOrCreateRoot(scene, LightingRootName).transform;
        ConfigureEnclosureForBaking(scene);
        GenerateMissingLightmapUVs(scene);
        ConfigureEnvironment(scene);

        FixtureStyle warm = new FixtureStyle(
            "Warm", new Color(1.00f, 0.86f, 0.62f), new Color(1.00f, 0.66f, 0.30f), 4.2f);
        FixtureStyle cyan = new FixtureStyle(
            "Cyan", new Color(0.55f, 0.94f, 1.00f), new Color(0.20f, 0.84f, 1.00f), 4.0f);
        FixtureStyle coral = new FixtureStyle(
            "Coral", new Color(1.00f, 0.57f, 0.48f), new Color(1.00f, 0.27f, 0.20f), 4.0f);
        FixtureStyle green = new FixtureStyle(
            "Green", new Color(0.62f, 1.00f, 0.64f), new Color(0.25f, 1.00f, 0.36f), 4.0f);
        FixtureStyle gold = new FixtureStyle(
            "Gold", new Color(1.00f, 0.89f, 0.35f), new Color(1.00f, 0.67f, 0.08f), 4.0f);

        FixtureStyle[,] styles =
        {
            { cyan, warm, coral },
            { warm, warm, warm },
            { green, warm, gold }
        };

        float[] coordinates = { -8.0f, 0.0f, 8.0f };
        int fixtureCount = 0;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                string fixtureName = $"CeilingFixture_R{row + 1}C{column + 1}";
                Vector3 position = new Vector3(coordinates[column], 11.72f, coordinates[2 - row]);
                ConfigureFixture(lightingRoot, fixtureName, position, styles[row, column]);
                fixtureCount++;
            }
        }

        ConfigureLightingSettings();
        ConfigureProbeVolumeBakingSet();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new
        {
            scene = scene.path,
            fixtureCount,
            root = LightingRootName,
            lightingSettings = LightingSettingsPath,
            ambientIntensity = RenderSettings.ambientIntensity,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            directionalLightDisabled = true
        };
    }

    private static void ConfigureFixture(
        Transform root,
        string fixtureName,
        Vector3 localPosition,
        FixtureStyle style)
    {
        Transform existing = root.Find(fixtureName);
        GameObject fixture = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        fixture.name = fixtureName;
        fixture.transform.SetParent(root, false);
        fixture.transform.localPosition = localPosition;
        fixture.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
        fixture.transform.localScale = new Vector3(3.15f, 3.15f, 1.0f);

        MeshFilter meshFilter = fixture.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = fixture.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = GetQuadMesh();

        Collider collider = fixture.GetComponent<Collider>();
        if (collider != null)
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        MeshRenderer renderer = fixture.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateMaterial(style);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.receiveGI = ReceiveGI.Lightmaps;
        renderer.scaleInLightmap = 0.25f;

        StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(fixture);
        flags |= StaticEditorFlags.ContributeGI | StaticEditorFlags.ReflectionProbeStatic;
        GameObjectUtility.SetStaticEditorFlags(fixture, flags);

        Transform lightTransform = fixture.transform.Find("BakedSpotlight");
        GameObject lightObject;
        if (lightTransform == null)
        {
            lightObject = new GameObject("BakedSpotlight");
            lightObject.transform.SetParent(fixture.transform, false);
        }
        else
        {
            lightObject = lightTransform.gameObject;
        }

        lightObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.05f);
        lightObject.transform.localRotation = Quaternion.identity;
        lightObject.transform.localScale = Vector3.one;

        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }

        if (lightObject.GetComponent<UniversalAdditionalLightData>() == null)
        {
            lightObject.AddComponent<UniversalAdditionalLightData>();
        }

        light.type = LightType.Spot;
        light.lightmapBakeType = LightmapBakeType.Baked;
        light.color = style.LightColor;
        light.intensity = style.Intensity;
        light.range = 15.0f;
        light.spotAngle = 82.0f;
        light.innerSpotAngle = 58.0f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.78f;
        light.shadowBias = 0.04f;
        light.shadowNormalBias = 0.25f;
        light.renderMode = LightRenderMode.Auto;
        light.useColorTemperature = false;
    }

    private static Mesh GetQuadMesh()
    {
        if (s_QuadMesh != null)
        {
            return s_QuadMesh;
        }

        GameObject temporaryQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        s_QuadMesh = temporaryQuad.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporaryQuad);
        return s_QuadMesh;
    }

    private static Material GetOrCreateMaterial(FixtureStyle style)
    {
        string path = $"{MaterialFolder}/CeilingLight_{style.Name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("The URP Lit shader is unavailable.");
            }

            material = new Material(shader) { name = $"CeilingLight_{style.Name}" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", style.PanelColor);
        material.SetColor("_EmissionColor", style.LightColor * 2.25f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureEnclosureForBaking(Scene scene)
    {
        string[] names = { "Plane", "Plane (1)", "Plane (2)", "Plane (3)", "Plane (4)", "Plane (5)" };
        foreach (string objectName in names)
        {
            GameObject enclosureObject = FindRootObject(scene, objectName);
            if (enclosureObject == null)
            {
                continue;
            }

            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(enclosureObject);
            flags |= StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(enclosureObject, flags);

            MeshRenderer renderer = enclosureObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = objectName == "Plane (5)" ? 0.5f : 1.0f;
            }
        }
    }

    private static void ConfigureEnvironment(Scene scene)
    {
        GameObject directionalObject = FindRootObject(scene, "Directional Light");
        if (directionalObject != null)
        {
            Light directionalLight = directionalObject.GetComponent<Light>();
            if (directionalLight != null)
            {
                directionalLight.enabled = false;
                EditorUtility.SetDirty(directionalLight);
            }
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 0.28f;
        RenderSettings.reflectionIntensity = 0.45f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
    }

    private static void GenerateMissingLightmapUVs(Scene scene)
    {
        string[] names = { "Plane", "Plane (1)", "Plane (2)", "Plane (3)", "Plane (4)", "Plane (5)" };
        foreach (string objectName in names)
        {
            GameObject enclosureObject = FindRootObject(scene, objectName);
            MeshFilter meshFilter = enclosureObject != null ? enclosureObject.GetComponent<MeshFilter>() : null;
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null || (mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount))
            {
                continue;
            }

            UnwrapParam unwrap = new UnwrapParam();
            UnwrapParam.SetDefaults(out unwrap);
            unwrap.hardAngle = 88.0f;
            unwrap.packMargin = 4.0f / 1024.0f;
            Unwrapping.GenerateSecondaryUVSet(mesh, unwrap);
            EditorUtility.SetDirty(mesh);
        }
    }

    private static void ConfigureLightingSettings()
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "MarioPartyLightingSettings" };
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.UnityComputeGPU;
        settings.lightmapResolution = 18.0f;
        settings.lightmapMaxSize = 1024;
        settings.lightmapPadding = 3;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 128;
        settings.maxBounces = 2;
        settings.indirectScale = 1.15f;
        settings.albedoBoost = 1.1f;
        settings.ao = true;
        settings.aoMaxDistance = 2.5f;
        settings.aoExponentIndirect = 1.25f;
        settings.lightmapCompression = LightmapCompression.HighQuality;
        settings.filteringMode = LightingSettings.FilterMode.Auto;

        Lightmapping.lightingSettings = settings;
        EditorUtility.SetDirty(settings);
    }

    private static void ConfigureProbeVolumeBakingSet()
    {
        UnityEngine.Object bakingSet = AssetDatabase.LoadMainAssetAtPath(BakingSetPath);
        if (bakingSet == null)
        {
            return;
        }

        SerializedObject serializedSet = new SerializedObject(bakingSet);
        SetBool(serializedSet, "skyOcclusion", true);
        SetBool(serializedSet, "settings.dilationSettings.enableDilation", true);
        SetFloat(serializedSet, "settings.dilationSettings.dilationDistance", 1.0f);
        SetBool(serializedSet, "settings.virtualOffsetSettings.useVirtualOffset", true);
        serializedSet.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bakingSet);
    }

    private static void SetBool(SerializedObject target, string propertyPath, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyPath);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    private static void SetFloat(SerializedObject target, string propertyPath, float value)
    {
        SerializedProperty property = target.FindProperty(propertyPath);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static GameObject FindOrCreateRoot(Scene scene, string objectName)
    {
        GameObject found = FindRootObject(scene, objectName);
        if (found != null)
        {
            return found;
        }

        GameObject created = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    private static GameObject FindRootObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }
        }

        return null;
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private readonly struct FixtureStyle
    {
        public FixtureStyle(string name, Color panelColor, Color lightColor, float intensity)
        {
            Name = name;
            PanelColor = panelColor;
            LightColor = lightColor;
            Intensity = intensity;
        }

        public string Name { get; }
        public Color PanelColor { get; }
        public Color LightColor { get; }
        public float Intensity { get; }
    }
}
