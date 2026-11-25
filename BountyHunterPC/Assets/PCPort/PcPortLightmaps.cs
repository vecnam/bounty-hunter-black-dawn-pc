using UnityEngine;

// Unity 4 lightmaps back. Unity 6 only applies lightmaps from a LightingDataAsset (which cannot be built from a
// script); without one every renderer has lightmapIndex -1 and the scenery has no baked light. tools/fix_lightmaps.py
// injects this component into the YAML of the scenes (with the original LightmapSettings textures) and of the mission
// streaming blocks (indices only: the lightmaps are the open scene's). Runs in Start, after Unity resets the indices on load.
public class PcPortLightmaps : MonoBehaviour
{
    public Texture2D[] lightmaps;
    public Renderer[] renderers;
    public int[] indices;
    public Vector4[] scaleOffsets;

    void Start()
    {
        if (lightmaps != null && lightmaps.Length > 0)
        {
            var data = new LightmapData[lightmaps.Length];
            for (int i = 0; i < data.Length; i++)
                data[i] = new LightmapData { lightmapColor = lightmaps[i] };
            LightmapSettings.lightmapsMode = LightmapsMode.NonDirectional;
            LightmapSettings.lightmaps = data;
        }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
            {
                renderers[i].lightmapIndex = indices[i];
                if (!renderers[i].isPartOfStaticBatch) // in the static batch the scale/offset is already baked into the combined mesh's UVs
                    renderers[i].lightmapScaleOffset = scaleOffsets[i];
            }
    }
}
