using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Looks up the exact GUIDs and fileIDs of the Unity 6 built-in shaders that match the Unity 4 ones.
public static class PcPortBuiltinShaderMap
{
    public static void Dump()
    {
        string[] names =
        {
            "Legacy Shaders/Diffuse",
            "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Particles/Additive",
            "Legacy Shaders/Particles/Alpha Blended"
        };
        var sb = new StringBuilder();
        foreach (string n in names)
        {
            Shader s = Shader.Find(n);
            if (s != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long fid))
                sb.AppendLine($"{n}\tguid={guid}\tfileID={fid}");
            else
                sb.AppendLine($"{n}\tMISSING");
        }
        string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/builtin_shaders.log"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllText(outPath, sb.ToString());
        Debug.Log("PcPortBuiltinShaderMap:\n" + sb);
    }
}
