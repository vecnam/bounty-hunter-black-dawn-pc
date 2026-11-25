using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Converts the 18 legacy particle emitters (EllipsoidParticleEmitter/ParticleAnimator/ParticleRenderer)
// into ParticleSystem + ParticleSystemRenderer in the project's 10 prefabs.
public static class PcPortParticleConverter
{
    private static readonly string[] TargetPrefabs =
    {
        "Assets/Resources/effect/bug_3_bom.prefab",
        "Assets/Resources/effect/enemy/boss/mercenaryboss/MercenaryBossRpgBullet.prefab",
        "Assets/Resources/effect/enemy/boss/mercenaryboss/MercenaryBossRpgBulletNew.prefab",
        "Assets/Resources/effect/enemy/boss/mercenaryboss/MercenaryBossRpgExplosion.prefab",
        "Assets/Resources/level/Rocket.prefab",
        "Assets/Resources/level/sc_03_fire1.prefab",
        "Assets/Resources/level/StartMenuScene.prefab",
        "Assets/Resources/loot/item_enegy.prefab",
        "Assets/Resources/loot/item_gold.prefab",
        "Assets/Resources/rpg_effect/RPG_Revolver_001.prefab",
    };

    private sealed class EmitterSpec
    {
        public int[] Path;
        public bool Enabled = true, Emit = true, RndRotation, SimulateWorld, OneShot, Autodestruct, AnimateColor;
        public float MinSize = 1f, MaxSize = 1f, MinEnergy = 1f, MaxEnergy = 1f, MinEmission = 10f, MaxEmission = 10f;
        public float AngularVel, RndAngularVel, MinEmitterRange, SizeGrow, Damping = 1f;
        public Vector3 WorldVel, LocalVel, RndVel, TangentVel, Ellipsoid, Force, RndForce;
        public uint[] Colors = new uint[5];
        public string MaterialGuid;
        public bool CastShadows = true, ReceiveShadows = true;
        public int StretchParticles, XTile = 1, YTile = 1, Cycles = 1;
        public float CameraVelScale, LengthScale = 2f, VelScale, MaxParticleSize = 0.25f;
    }

    private static float F(string body, string key, float def = 0f)
    {
        var m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*([-+0-9.eE]+)", RegexOptions.Multiline);
        return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : def;
    }

    private static int I(string body, string key, int def = 0)
    {
        var m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*(-?\d+)", RegexOptions.Multiline);
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : def;
    }

    private static Vector3 V3(string body, string key)
    {
        var m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*\{x:\s*([-+0-9.eE]+),\s*y:\s*([-+0-9.eE]+),\s*z:\s*([-+0-9.eE]+)\}", RegexOptions.Multiline);
        if (!m.Success) return Vector3.zero;
        return new Vector3(
            float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    private static int[] FindChildPath(string tfId, Dictionary<string, (string father, List<string> children)> tfs)
    {
        var path = new List<int>();
        string cur = tfId;
        while (tfs.TryGetValue(cur, out var node) && node.father != "0")
        {
            var parent = tfs[node.father];
            path.Add(parent.children.IndexOf(cur));
            cur = node.father;
        }
        path.Reverse();
        return path.ToArray();
    }

    public static void ConvertAll()
    {
        int totalConverted = 0;
        foreach (string rel in TargetPrefabs)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", rel));
            if (!File.Exists(full))
            {
                Debug.LogWarning("Prefab not found: " + rel);
                continue;
            }
            string yaml = File.ReadAllText(full);
            if (!Regex.IsMatch(yaml, @"^--- !u!(12|15|26)\b", RegexOptions.Multiline))
            {
                Debug.Log("Already converted: " + rel);
                continue;
            }

            var blocks = Regex.Split(yaml, @"(?=^--- !u!\d+ &\d+)", RegexOptions.Multiline);
            var tfs = new Dictionary<string, (string father, List<string> children)>();
            var goToTf = new Dictionary<string, string>();
            var specsByGo = new Dictionary<string, EmitterSpec>();

            foreach (string b in blocks)
            {
                var hm = Regex.Match(b, @"^--- !u!(\d+) &(\d+)\s*\r?\n([\s\S]*)");
                if (!hm.Success) continue;
                string cid = hm.Groups[1].Value;
                string fid = hm.Groups[2].Value;
                string body = hm.Groups[3].Value;
                if (cid == "4")
                {
                    string go = Regex.Match(body, @"m_GameObject:\s*\{fileID:\s*(\d+)\}").Groups[1].Value;
                    string father = Regex.Match(body, @"m_Father:\s*\{fileID:\s*(\d+)\}").Groups[1].Value;
                    var children = new List<string>();
                    var cm = Regex.Match(body, @"m_Children:([\s\S]*?)(?:\r?\n  m_|\Z)");
                    if (cm.Success)
                        foreach (Match ch in Regex.Matches(cm.Groups[1].Value, @"fileID:\s*(\d+)"))
                            children.Add(ch.Groups[1].Value);
                    tfs[fid] = (father, children);
                    goToTf[go] = fid;
                }
                else if (cid == "15" || cid == "12" || cid == "26")
                {
                    string go = Regex.Match(body, @"m_GameObject:\s*\{fileID:\s*(\d+)\}").Groups[1].Value;
                    if (!specsByGo.TryGetValue(go, out var sp))
                    {
                        sp = new EmitterSpec();
                        specsByGo[go] = sp;
                    }
                    if (cid == "15")
                    {
                        sp.Enabled = I(body, "m_Enabled", 1) != 0;
                        sp.Emit = I(body, "m_Emit", 1) != 0;
                        sp.MinSize = F(body, "minSize", 1f);
                        sp.MaxSize = F(body, "maxSize", 1f);
                        sp.MinEnergy = F(body, "minEnergy", 1f);
                        sp.MaxEnergy = F(body, "maxEnergy", 1f);
                        sp.MinEmission = F(body, "minEmission", 10f);
                        sp.MaxEmission = F(body, "maxEmission", 10f);
                        sp.WorldVel = V3(body, "worldVelocity");
                        sp.LocalVel = V3(body, "localVelocity");
                        sp.RndVel = V3(body, "rndVelocity");
                        sp.TangentVel = V3(body, "tangentVelocity");
                        sp.AngularVel = F(body, "angularVelocity");
                        sp.RndAngularVel = F(body, "rndAngularVelocity");
                        sp.RndRotation = I(body, "rndRotation") != 0;
                        sp.SimulateWorld = I(body, "Simulate in Worldspace?") != 0;
                        sp.OneShot = I(body, "m_OneShot") != 0;
                        sp.Ellipsoid = V3(body, "m_Ellipsoid");
                        sp.MinEmitterRange = F(body, "m_MinEmitterRange");
                    }
                    else if (cid == "12")
                    {
                        sp.AnimateColor = I(body, "Does Animate Color?") != 0;
                        for (int k = 0; k < 5; k++)
                        {
                            var cm = Regex.Match(body, $@"colorAnimation\[{k}\]:\s*\r?\n\s*serializedVersion:\s*\d+\s*\r?\n\s*rgba:\s*(\d+)");
                            if (cm.Success) sp.Colors[k] = uint.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture);
                        }
                        sp.SizeGrow = F(body, "sizeGrow");
                        sp.RndForce = V3(body, "rndForce");
                        sp.Force = V3(body, "force");
                        sp.Damping = F(body, "damping", 1f);
                        sp.Autodestruct = I(body, "autodestruct") != 0;
                    }
                    else if (cid == "26")
                    {
                        sp.CastShadows = I(body, "m_CastShadows", 1) != 0;
                        sp.ReceiveShadows = I(body, "m_ReceiveShadows", 1) != 0;
                        var mm = Regex.Match(body, @"m_Materials:\s*\r?\n\s*-\s*\{[^}]*guid:\s*([0-9a-f]+)");
                        if (mm.Success) sp.MaterialGuid = mm.Groups[1].Value;
                        sp.CameraVelScale = F(body, "m_CameraVelocityScale");
                        sp.StretchParticles = I(body, "m_StretchParticles");
                        sp.LengthScale = F(body, "m_LengthScale", 2f);
                        sp.VelScale = F(body, "m_VelocityScale");
                        sp.MaxParticleSize = F(body, "m_MaxParticleSize", 0.25f);
                        sp.XTile = I(body, "x Tile", 1);
                        sp.YTile = I(body, "y Tile", 1);
                        sp.Cycles = I(body, "cycles", 1);
                    }
                }
            }

            foreach (var kv in specsByGo)
                kv.Value.Path = FindChildPath(goToTf[kv.Key], tfs);

            // Remove blocks 12/15/26 and their m_Component references before loading in Unity 6.
            var kept = blocks
                .Where(b => !Regex.IsMatch(b, @"^--- !u!(12|15|26)\b"))
                .Select(b => Regex.Replace(b, @"^\s*-\s*(?:12|15|26):\s*\{fileID:\s*\d+\}\r?\n", "", RegexOptions.Multiline));
            File.WriteAllText(full, string.Concat(kept));
            AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);

            GameObject root = PrefabUtility.LoadPrefabContents(rel);
            try
            {
                foreach (var sp in specsByGo.Values)
                {
                    Transform target = root.transform;
                    foreach (int idx in sp.Path)
                        target = target.GetChild(idx);
                    ApplyParticleSystem(target.gameObject, sp);
                    totalConverted++;
                }
                foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tr.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, rel);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"Converted {rel}: {specsByGo.Count} emitters");
        }
        Debug.Log($"PcPortParticleConverter: total converted = {totalConverted}");
    }

    private static void ApplyParticleSystem(GameObject go, EmitterSpec sp)
    {
        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null) ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = sp.Enabled && sp.Emit;
        main.loop = !sp.OneShot;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, sp.MinEnergy), Mathf.Max(0.01f, sp.MaxEnergy));
        main.startSize = new ParticleSystem.MinMaxCurve(sp.MinSize, sp.MaxSize);
        main.startSpeed = 0f;
        main.simulationSpace = sp.SimulateWorld ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.startRotation = sp.RndRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f);
        main.stopAction = sp.Autodestruct ? ParticleSystemStopAction.Destroy : ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        if (sp.OneShot)
        {
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(sp.MinEmission), (short)Mathf.RoundToInt(sp.MaxEmission))
            });
        }
        else
        {
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(sp.MinEmission, sp.MaxEmission);
        }

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        if (sp.Ellipsoid == Vector3.zero)
        {
            shape.radius = 0.01f;
            shape.scale = Vector3.one;
        }
        else
        {
            shape.radius = 1f;
            shape.scale = sp.Ellipsoid;
        }
        shape.radiusThickness = Mathf.Clamp01(1f - sp.MinEmitterRange);

        bool hasVel = sp.LocalVel != Vector3.zero || sp.WorldVel != Vector3.zero || sp.RndVel != Vector3.zero || sp.TangentVel != Vector3.zero;
        var vel = ps.velocityOverLifetime;
        vel.enabled = hasVel;
        if (hasVel)
        {
            bool useWorld = sp.WorldVel != Vector3.zero && sp.LocalVel == Vector3.zero;
            vel.space = useWorld ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            Vector3 vBase = useWorld ? sp.WorldVel : sp.LocalVel + sp.WorldVel;
            Vector3 vr = new Vector3(Mathf.Abs(sp.RndVel.x), Mathf.Abs(sp.RndVel.y), Mathf.Abs(sp.RndVel.z));
            vel.x = new ParticleSystem.MinMaxCurve(vBase.x - vr.x, vBase.x + vr.x);
            vel.y = new ParticleSystem.MinMaxCurve(vBase.y - vr.y, vBase.y + vr.y);
            vel.z = new ParticleSystem.MinMaxCurve(vBase.z - vr.z, vBase.z + vr.z);
            vel.orbitalY = sp.TangentVel.x;
            vel.radial = sp.TangentVel.z;
        }

        bool hasRot = Mathf.Abs(sp.AngularVel) > 0.001f || Mathf.Abs(sp.RndAngularVel) > 0.001f;
        var rot = ps.rotationOverLifetime;
        rot.enabled = hasRot;
        if (hasRot)
        {
            float rMin = (sp.AngularVel - Mathf.Abs(sp.RndAngularVel)) * Mathf.Deg2Rad;
            float rMax = (sp.AngularVel + Mathf.Abs(sp.RndAngularVel)) * Mathf.Deg2Rad;
            rot.z = new ParticleSystem.MinMaxCurve(rMin, rMax);
        }

        var col = ps.colorOverLifetime;
        col.enabled = sp.AnimateColor;
        if (sp.AnimateColor)
        {
            var grad = new Gradient();
            var cKeys = new GradientColorKey[5];
            var aKeys = new GradientAlphaKey[5];
            for (int k = 0; k < 5; k++)
            {
                uint rgba = sp.Colors[k];
                Color32 c32 = new Color32(
                    (byte)(rgba & 0xFF),
                    (byte)((rgba >> 8) & 0xFF),
                    (byte)((rgba >> 16) & 0xFF),
                    (byte)((rgba >> 24) & 0xFF));
                float t = k * 0.25f;
                cKeys[k] = new GradientColorKey(c32, t);
                aKeys[k] = new GradientAlphaKey(c32.a / 255f, t);
            }
            grad.SetKeys(cKeys, aKeys);
            col.color = grad;
        }

        var sol = ps.sizeOverLifetime;
        sol.enabled = Mathf.Abs(sp.SizeGrow) > 0.001f;
        if (sol.enabled)
        {
            float avgLife = Mathf.Max(0.05f, (sp.MinEnergy + sp.MaxEnergy) * 0.5f);
            float endScale = Mathf.Max(0.05f, 1f + sp.SizeGrow * avgLife);
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endScale));
        }

        bool hasForce = sp.Force != Vector3.zero || sp.RndForce != Vector3.zero;
        var force = ps.forceOverLifetime;
        force.enabled = hasForce;
        if (hasForce)
        {
            Vector3 rf = new Vector3(Mathf.Abs(sp.RndForce.x), Mathf.Abs(sp.RndForce.y), Mathf.Abs(sp.RndForce.z));
            force.x = new ParticleSystem.MinMaxCurve(sp.Force.x - rf.x, sp.Force.x + rf.x);
            force.y = new ParticleSystem.MinMaxCurve(sp.Force.y - rf.y, sp.Force.y + rf.y);
            force.z = new ParticleSystem.MinMaxCurve(sp.Force.z - rf.z, sp.Force.z + rf.z);
        }

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = sp.Damping < 0.999f;
        if (limit.enabled)
        {
            limit.limit = 999f;
            limit.drag = Mathf.Max(0f, 1f - sp.Damping);
        }

        var tsa = ps.textureSheetAnimation;
        tsa.enabled = sp.XTile > 1 || sp.YTile > 1;
        if (tsa.enabled)
        {
            tsa.numTilesX = Math.Max(1, sp.XTile);
            tsa.numTilesY = Math.Max(1, sp.YTile);
            tsa.cycleCount = Math.Max(1, sp.Cycles);
        }

        var ren = go.GetComponent<ParticleSystemRenderer>();
        if (ren != null)
        {
            Material mat = null;
            if (!string.IsNullOrEmpty(sp.MaterialGuid))
            {
                string mPath = AssetDatabase.GUIDToAssetPath(sp.MaterialGuid);
                if (!string.IsNullOrEmpty(mPath))
                    mat = AssetDatabase.LoadAssetAtPath<Material>(mPath);
            }
            if (mat == null && sp.Enabled)
                mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            ren.sharedMaterial = mat;
            ren.renderMode = sp.StretchParticles != 0 ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            ren.lengthScale = sp.LengthScale;
            ren.velocityScale = sp.VelScale;
            ren.cameraVelocityScale = sp.CameraVelScale;
            ren.maxParticleSize = sp.MaxParticleSize;
            ren.shadowCastingMode = sp.CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            ren.receiveShadows = sp.ReceiveShadows;
        }
    }
}
