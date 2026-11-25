using UnityEngine;

// Replaces the Unity 4 AssetBundle: the Android bundles do not load on PC, so the content of each one was moved
// to Resources/bundles/<original path> (tools/map_bundles.py) and the game gets the same mainAsset/Unload as before.
public sealed class PcPortBundle
{
    const string Prefix = "android_prefab_";

    public readonly Object mainAsset;

    private PcPortBundle(Object mainAsset)
    {
        this.mainAsset = mainAsset;
    }

    // Never returns null: the SceneStreamingScript loop only moves on when mAssetBundle != null.
    // Scenery shader/texture/material bundles have no content in the port (the prefabs reference them directly),
    // so a null mainAsset is normal for them; blocks and enemies were moved to Resources/bundles by tools/map_bundles.py.
    public static PcPortBundle Load(string name)
    {
        Object asset = Resources.Load("bundles/" + name);
        bool sceneDependency = name.StartsWith("scenes/") && (name.Contains("/shaders/") || name.Contains("/textures/") || name.Contains("/materials/"));
        if (asset == null && !sceneDependency)
            Debug.LogWarning("[PcPortBundle] no content for " + name);
        // Unity names the prefab root after the file (android_prefab_<original name>); in Unity 4 the mainAsset had the
        // original name, and the game finds instances by it (chests: chestDropConfigName["instance1-2(Clone)"]; without it a
        // chest found no config and dropped nothing). In the 245 mapped prefabs, removing the prefix gives the exact name.
        if (asset is GameObject && asset.name.StartsWith(Prefix))
            asset.name = asset.name.Substring(Prefix.Length);
        return new PcPortBundle(asset);
    }

    // shortcut: Resources cannot unload a single prefab; memory comes back on scene change (UnloadUnusedAssets). Fine on PC.
    public void Unload(bool unloadAllLoadedObjects)
    {
    }
}
