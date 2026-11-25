using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Generates the development gallery scene (Assets/PCPort/Scenes/ShaderGallery.unity) to look at every ported
// shader, characters, scenery and converted particles. Only included in the dev build.
public static class PcPortGallery
{
    public const string ScenePath = "Assets/PCPort/Scenes/ShaderGallery.unity";

    public static string EnsureGalleryScene()
    {
        Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "PCPort/Scenes")));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lighting and camera
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.16f, 0.2f, 1f);
        camGo.transform.position = new Vector3(0f, 3.5f, -15f);
        camGo.transform.LookAt(new Vector3(0f, 1.5f, 0f));

        // 1) Grid of spheres/quads: one material per shader in Assets/Shader + built-ins
        var matByShader = new Dictionary<string, Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Material", "Assets/Resources", "Assets/AssetBundles" }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (mat == null || mat.shader == null) continue;
            string sn = mat.shader.name;
            if (!matByShader.ContainsKey(sn))
                matByShader[sn] = mat;
        }

        var sortedShaders = matByShader.OrderBy(kv => kv.Key).ToList();
        int cols = 9;
        for (int i = 0; i < sortedShaders.Count; i++)
        {
            int r = i / cols;
            int c = i % cols;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Shader_" + sortedShaders[i].Key.Replace("/", "_");
            sphere.transform.position = new Vector3((c - (cols - 1) * 0.5f) * 1.6f, 6.2f - r * 1.4f, 0f);
            sphere.transform.localScale = Vector3.one * 1.1f;
            sphere.GetComponent<Renderer>().sharedMaterial = sortedShaders[i].Value;
        }

        // 2) Characters and a scenery block
        PlacePrefab("Assets/Resources/enemy/gungun.prefab", new Vector3(-5.5f, -3.5f, -2f), Vector3.one * 1.5f);
        PlacePrefab("Assets/Resources/enemy/knife01.prefab", new Vector3(-3.0f, -3.5f, -2f), Vector3.one * 1.5f);
        PlacePrefab("Assets/AssetBundles/android_prefab_b_float.assetbundle/FLOAT.prefab", new Vector3(0.0f, -3.5f, -2f), Vector3.one * 1.2f);
        PlacePrefab("Assets/AssetBundles/android_prefab_instance_04_block00.assetbundle/instance_04_block00.prefab", new Vector3(6.0f, -4.5f, 4f), Vector3.one * 0.25f);

        // 3) Converted particles (looping turned on in the gallery so they show in the screenshot)
        PlaceParticle("Assets/Resources/loot/item_gold.prefab", new Vector3(-4.5f, -1.5f, -5f));
        PlaceParticle("Assets/Resources/loot/item_enegy.prefab", new Vector3(-2.0f, -1.5f, -5f));
        PlaceParticle("Assets/Resources/effect/bug_3_bom.prefab", new Vector3(0.5f, -1.5f, -5f));
        PlaceParticle("Assets/Resources/level/sc_03_fire1.prefab", new Vector3(3.0f, -1.5f, -5f));
        PlaceParticle("Assets/Resources/rpg_effect/RPG_Revolver_001.prefab", new Vector3(5.5f, -1.5f, -5f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        return ScenePath;
    }

    private static void PlacePrefab(string assetPath, Vector3 pos, Vector3 scale)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private static void PlaceParticle(string assetPath, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = pos;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            m.loop = true;
            m.playOnAwake = true;
            m.stopAction = ParticleSystemStopAction.None;
        }
    }
}
