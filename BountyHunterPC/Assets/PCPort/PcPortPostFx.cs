using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// Remaster: post-processing (Post Processing v2 package) on the world camera only, the FPSCamera, which also draws
// the weapon. The NGUI cameras draw on top afterwards, so the UI does not change. Light effects: ambient occlusion,
// bloom on near-white only, and a bit more contrast and saturation. F9 toggles it.
// The package shaders come from the Resources/PcPortPostFx asset: without a reference, the build would leave them out.
public class PcPortPostFx : MonoBehaviour
{
    PostProcessResources resources;
    PostProcessVolume volume;
    bool on = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        PcPortPostFxResources holder = Resources.Load<PcPortPostFxResources>("PcPortPostFx");
        if (holder == null || holder.resources == null)
        {
            Debug.LogWarning("[PcPortPostFx] no Resources/PcPortPostFx: post-processing off");
            return;
        }
        var go = new GameObject("PcPortPostFx");
        DontDestroyOnLoad(go);
        PcPortPostFx fx = go.AddComponent<PcPortPostFx>();
        fx.resources = holder.resources;
        fx.volume = PostProcessManager.instance.QuickVolume(0, 100f, Effects());
        DontDestroyOnLoad(fx.volume.gameObject);
        // the FPSCamera is created with the player (city, mission): look for it every now and then
        fx.InvokeRepeating(nameof(Attach), 0f, 0.5f);
    }

    static PostProcessEffectSettings[] Effects()
    {
        var ao = ScriptableObject.CreateInstance<AmbientOcclusion>();
        ao.enabled.Override(true);
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(0.6f);
        var bloom = ScriptableObject.CreateInstance<Bloom>();
        bloom.enabled.Override(true);
        // gamma and no HDR: high threshold and short knee, or sunlit ground (lightmap x2) turns into fog
        bloom.intensity.Override(0.6f);
        bloom.threshold.Override(0.95f);
        bloom.softKnee.Override(0.1f);
        var grading = ScriptableObject.CreateInstance<ColorGrading>();
        grading.enabled.Override(true);
        grading.gradingMode.Override(GradingMode.LowDefinitionRange);
        grading.contrast.Override(10f);
        grading.saturation.Override(10f);
        return new PostProcessEffectSettings[] { ao, bloom, grading };
    }

    void Attach()
    {
        foreach (Camera cam in Camera.allCameras)
            if (cam.name == "FPSCamera" && cam.GetComponent<PostProcessLayer>() == null)
            {
                PostProcessLayer layer = cam.gameObject.AddComponent<PostProcessLayer>();
                layer.Init(resources);
                layer.volumeLayer = 1 << volume.gameObject.layer;
                layer.antialiasingMode = PostProcessLayer.Antialiasing.None; // the 8x MSAA in QualitySettings already handles it
                layer.enabled = on;
                Debug.Log("[PcPortPostFx] enabled on " + cam.name);
            }
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F9))
            return;
        on = !on;
        foreach (PostProcessLayer layer in FindObjectsByType<PostProcessLayer>(FindObjectsSortMode.None))
            layer.enabled = on;
    }
}
