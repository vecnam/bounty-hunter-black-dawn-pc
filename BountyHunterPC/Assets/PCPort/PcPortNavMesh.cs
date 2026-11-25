using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Adds to the scene the NavMesh rebuilt by PcPortNavMeshBaker (Unity 6 cannot read the Unity 4 one).
// A NavMesh added from code does not belong to the scene: the previous scene's one is removed on every switch.
public static class PcPortNavMesh
{
    static readonly List<NavMeshDataInstance> data = new List<NavMeshDataInstance>();
    static readonly List<NavMeshLinkInstance> links = new List<NavMeshLinkInstance>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;
        foreach (NavMeshDataInstance d in data)
            d.Remove();
        foreach (NavMeshLinkInstance l in links)
            l.Remove();
        data.Clear();
        links.Clear();
        foreach (NavMeshData nav in Resources.LoadAll<NavMeshData>("navmesh/" + scene.name))
            data.Add(NavMesh.AddNavMeshData(nav));
        TextAsset txt = Resources.Load<TextAsset>("navmesh/" + scene.name + "/links");
        if (txt == null)
            return;
        // each line: start xyz, end xyz, area, bidirectional (0/1)
        foreach (string line in txt.text.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = line.Split(' ');
            float F(int i) => float.Parse(f[i], CultureInfo.InvariantCulture);
            links.Add(NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = new Vector3(F(0), F(1), F(2)),
                endPosition = new Vector3(F(3), F(4), F(5)),
                costModifier = -1f,
                area = int.Parse(f[6]),
                bidirectional = f[7] == "1",
            }));
        }
        Debug.Log("[PcPortNavMesh] " + scene.name + ": " + data.Count + " NavMeshData, " + links.Count + " links");
    }
}
