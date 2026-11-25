using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Unity 6 cannot read the Unity 4 NavMesh (class 194, Assets/NavMesh/NavMesh_*.asset) and every enemy failed with
// "no valid NavMesh". Here the Unity 4 Detour tiles (version 13) are decoded and the NavMesh is rebuilt from the original
// detail mesh, one NavMeshData per area (3 = ground, 4 = flying: kept apart so the flying one does not cut the ground
// below it), in Resources/navmesh/<scene>/. Drop links (area 2) go to links.txt; PcPortNavMesh loads everything.
// Usage: python -I -X utf8 tools/unity.py exec PcPortNavMeshBaker.BakeAll
public static class PcPortNavMeshBaker
{
    const float Voxel = 0.1f; // the original mesh is already eroded by the agent radius: only 1 voxel of erosion here

    public static void BakeAll()
    {
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            Match m = Regex.Match(File.ReadAllText(s.path), @"m_NavMesh: \{fileID: 19400000, guid: ([0-9a-f]+)");
            if (m.Success)
                Bake(Path.GetFileNameWithoutExtension(s.path), AssetDatabase.GUIDToAssetPath(m.Groups[1].Value));
        }
        AssetDatabase.SaveAssets();
    }

    static void Bake(string scene, string navPath)
    {
        Match hex = Regex.Match(File.ReadAllText(navPath), @"m_MeshData: ([0-9a-f]*)");
        var tris = new SortedDictionary<int, List<Vector3>>();
        var links = new StringBuilder();
        Decode(HexToBytes(hex.Groups[1].Value), tris, links);
        string dir = "Assets/Resources/navmesh/" + scene;
        if (AssetDatabase.IsValidFolder(dir))
            AssetDatabase.DeleteAsset(dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(dir + "/links.txt", links.ToString());
        AssetDatabase.ImportAsset(dir + "/links.txt");

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        settings.overrideVoxelSize = true;
        settings.voxelSize = Voxel;
        settings.agentRadius = Voxel;
        settings.agentHeight = 1f;  // the source is just the walkable surface: a low height avoids cutting overlapping floors
        settings.agentSlope = 60f;  // the polygons already went through the 45 degree filter in Unity 4
        settings.agentClimb = 0.4f; // agentClimb of the original NavMeshSettings
        foreach (KeyValuePair<int, List<Vector3>> area in tris)
        {
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(area.Value[0], Vector3.zero);
            foreach (List<Vector3> chunk in Chunks(Subdivide(area.Value)))
            {
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(chunk);
                var idx = new int[chunk.Count];
                for (int i = 0; i < idx.Length; i++)
                    idx[i] = i;
                mesh.SetTriangles(idx, 0);
                mesh.RecalculateBounds();
                bounds.Encapsulate(mesh.bounds);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = mesh, transform = Matrix4x4.identity, area = area.Key });
            }
            bounds.Expand(2f);
            foreach (string msg in settings.ValidationReport(bounds))
                Debug.LogWarning("[PcPortNavMeshBaker] " + scene + ": " + msg);
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
                throw new Exception("bake failed: " + scene + " area " + area.Key);
            AssetDatabase.CreateAsset(data, dir + "/area" + area.Key + ".asset");
            foreach (NavMeshBuildSource s in sources)
                UnityEngine.Object.DestroyImmediate(s.sourceObject);
            Debug.Log("[PcPortNavMeshBaker] " + scene + " (" + Path.GetFileName(navPath) + ") area " + area.Key + ": " + area.Value.Count / 3
                + " original triangles, " + SurfaceArea(area.Value).ToString("0") + " m2 original, " + Coverage(data, area.Key).ToString("0") + " m2 rebuilt");
        }
        Debug.Log("[PcPortNavMeshBaker] " + scene + ": " + links.ToString().Split('\n').Length.ToString() + " lines in links.txt");
    }

    // Unity 4 Detour tiles: a 40-byte NavMeshSetHeader ('MSET', version 1, tile count, dtNavMeshParams) and, per tile,
    // (tileRef, size) + data: a 104-byte header ('VAND', version 13), vertices, polygons (36 bytes), links (12),
    // detail meshes (12), detail vertices, detail triangles (4 x ushort), BV nodes (16) and off-mesh links (40).
    static void Decode(byte[] b, SortedDictionary<int, List<Vector3>> tris, StringBuilder links)
    {
        if (Encoding.ASCII.GetString(b, 0, 4) != "TESM" || I32(b, 4) != 1)
            throw new Exception("unexpected NavMeshSetHeader");
        int tiles = I32(b, 8), off = 40;
        for (int t = 0; t < tiles; t++)
        {
            int d = off + 8, end = d + I32(b, off + 4);
            off = end;
            if (Encoding.ASCII.GetString(b, d, 4) != "VAND" || I32(b, d + 4) != 13)
                throw new Exception("unexpected Detour tile");
            int pc = I32(b, d + 24), vc = I32(b, d + 28), lc = I32(b, d + 32), dmc = I32(b, d + 36), dvc = I32(b, d + 40), dtc = I32(b, d + 44), bvc = I32(b, d + 48), omc = I32(b, d + 52);
            int oV = d + 104, oP = oV + A4(12 * vc), oDm = oP + A4(36 * pc) + A4(12 * lc), oDv = oDm + A4(12 * dmc), oDt = oDv + A4(12 * dvc), oOm = oDt + A4(8 * dtc) + A4(16 * bvc);
            if (oOm + A4(40 * omc) != end)
                throw new Exception("tile size does not match the layout");
            for (int i = 0; i < pc; i++)
            {
                int p = oP + 36 * i, nv = b[p + 32], areaType = b[p + 33];
                if (areaType >> 6 != 0)
                    continue; // off-mesh link polygon
                if (!tris.TryGetValue(areaType & 0x3f, out List<Vector3> list))
                    tris[areaType & 0x3f] = list = new List<Vector3>();
                int dm = oDm + 12 * i, vb = I32(b, dm), tb = I32(b, dm + 4), dnt = U16(b, dm + 10);
                for (int k = 0; k < dnt; k++)
                    for (int c = 0; c < 3; c++)
                    {
                        int v = U16(b, oDt + 8 * (tb + k) + 2 * c);
                        list.Add(v < nv ? V3(b, oV + 12 * U16(b, p + 4 + 2 * v)) : V3(b, oDv + 12 * (vb + v - nv)));
                    }
            }
            for (int i = 0; i < omc; i++)
            {
                int o = oOm + 40 * i, poly = U16(b, o + 32);
                links.Append(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7}\n",
                    F32(b, o), F32(b, o + 4), F32(b, o + 8), F32(b, o + 12), F32(b, o + 16), F32(b, o + 20), b[oP + 36 * poly + 33] & 0x3f, b[o + 34] & 1));
            }
        }
        if (off != b.Length)
            throw new Exception("bytes left after the last tile");
    }

    // Unity's bake loses the bottom of a very tall source: with Instance5's area 3 in a single mesh (from -24 to
    // +66 m), everything below -11.8 m vanished (59% of the area). So each cell of Chunk meters becomes its own source,
    // triangles are split down to MaxEdge (or a 166 m polygon stretches the cell height) and the ones too steep
    // to walk are dropped (there is a detail spike up to y=1281 in Instance4).
    const float Chunk = 20f, MaxEdge = 5f;

    static IEnumerable<List<Vector3>> Chunks(List<Vector3> t)
    {
        var cells = new Dictionary<Vector2Int, List<Vector3>>();
        float minUp = Mathf.Cos(60f * Mathf.Deg2Rad);
        for (int i = 0; i < t.Count; i += 3)
        {
            if (Vector3.Cross(t[i + 1] - t[i], t[i + 2] - t[i]).normalized.y < minUp)
                continue;
            Vector3 c = (t[i] + t[i + 1] + t[i + 2]) / 3f;
            var key = new Vector2Int(Mathf.FloorToInt(c.x / Chunk), Mathf.FloorToInt(c.z / Chunk));
            if (!cells.TryGetValue(key, out List<Vector3> cell))
                cells[key] = cell = new List<Vector3>();
            cell.AddRange(t.GetRange(i, 3));
        }
        return cells.Values;
    }

    static List<Vector3> Subdivide(List<Vector3> src)
    {
        var dst = new List<Vector3>(src.Count);
        var todo = new Stack<Vector3>();
        for (int i = 0; i < src.Count; i += 3)
        {
            todo.Push(src[i + 2]);
            todo.Push(src[i + 1]);
            todo.Push(src[i]);
            while (todo.Count > 0)
            {
                Vector3 a = todo.Pop(), b = todo.Pop(), c = todo.Pop();
                float ab = (b - a).sqrMagnitude, bc = (c - b).sqrMagnitude, ca = (a - c).sqrMagnitude;
                if (Mathf.Max(ab, Mathf.Max(bc, ca)) <= MaxEdge * MaxEdge)
                {
                    dst.Add(a);
                    dst.Add(b);
                    dst.Add(c);
                    continue;
                }
                Vector3[] halves = ab >= bc && ab >= ca ? new[] { a, (a + b) / 2f, c, (a + b) / 2f, b, c }
                    : bc >= ca ? new[] { a, b, (b + c) / 2f, a, (b + c) / 2f, c }
                    : new[] { a, b, (c + a) / 2f, (c + a) / 2f, b, c };
                for (int k = halves.Length - 1; k >= 0; k--)
                    todo.Push(halves[k]);
            }
        }
        return dst;
    }

    static float SurfaceArea(List<Vector3> t)
    {
        float a = 0f;
        for (int i = 0; i < t.Count; i += 3)
            a += Vector3.Cross(t[i + 1] - t[i], t[i + 2] - t[i]).magnitude / 2f;
        return a;
    }

    // Area (m2) of the rebuilt NavMesh: compared with the original one in the log, it shows how much erosion and filters ate.
    static float Coverage(NavMeshData data, int area)
    {
        NavMeshDataInstance inst = NavMesh.AddNavMeshData(data);
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        inst.Remove();
        float a = 0f;
        for (int i = 0; i < tri.areas.Length; i++)
            if (tri.areas[i] == area)
                a += Vector3.Cross(tri.vertices[tri.indices[3 * i + 1]] - tri.vertices[tri.indices[3 * i]], tri.vertices[tri.indices[3 * i + 2]] - tri.vertices[tri.indices[3 * i]]).magnitude / 2f;
        return a;
    }

    static int A4(int x) { return (x + 3) & ~3; }
    static int I32(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
    static int U16(byte[] b, int o) { return BitConverter.ToUInt16(b, o); }
    static float F32(byte[] b, int o) { return BitConverter.ToSingle(b, o); }
    static Vector3 V3(byte[] b, int o) { return new Vector3(F32(b, o), F32(b, o + 4), F32(b, o + 8)); }

    static byte[] HexToBytes(string hex)
    {
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);
        return b;
    }
}
