using System.Collections.Generic;
using UnityEngine;

// Circular PC minimap in the top right corner:
// - hides the top GPS bar (HotKey/Anchor-Top/Map and StateMap) without touching the prefabs;
// - turns with the camera (forward always up, view cone), with markers for quests (pinned to the edge when far),
//   NPCs with an available quest, portals, city shops/machines and active enemies;
// - Tab shows/hides the minimap; the mission strip ("Set 0/6 CCTVs") moves below the circle while it is visible.
[DefaultExecutionOrder(50)]
public class PcPortMinimap : MonoBehaviour
{
    const float Margin = 14f;
    const float Diameter = 148f;
    const float Radius = Diameter * 0.5f;
    const float MissionGap = 10f;
    const float MissionHalfHeight = 33f;
    const float MapRadiusPx = 155f; // radius shown, in MiniMapSize pixels (about 1100x764)
    const float InnerLimit = 0.89f;
    const float RimClamp = 0.93f;

    static readonly Color EnemyColor = new Color(1f, 0.22f, 0.22f, 1f);
    static readonly Color AllyColor = new Color(0.25f, 0.9f, 1f, 1f);

    public static bool IsUserVisible { get; private set; } = true;
    public static PcPortMinimap Instance { get; private set; }

    HUDManager builtHud;
    HotKeyManager hk;
    Vector3 missionOrigPos;

    GameObject root;
    UIPanel panel;
    UITexture mapWidget;
    Material mapMat;
    Material dotMat;
    Texture2D dotTex;
    UIAtlas iconAtlas;
    UISprite playerArrow;

    readonly List<UISprite> iconPool = new List<UISprite>();
    readonly List<UITexture> dotPool = new List<UITexture>();
    int usedIcons;
    int usedDots;

    string loadedSceneFile = "";
    Texture mapTex;
    GameScene cachedPoiScene;
    GameObject[] cachedGambles;
    GameObject[] cachedUpgrades;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("PcPortMinimap");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PcPortMinimap>();
    }

    public string DebugSummary()
    {
        string texName = mapTex != null ? mapTex.name : "null";
        string active = root != null ? root.activeInHierarchy.ToString() : "null";
        string my = hk != null && hk.m_Mission != null ? hk.m_Mission.transform.localPosition.y.ToString("0.0") : "-";
        return "active=" + active + " userVisible=" + IsUserVisible + " scene=" + loadedSceneFile
            + " tex=" + texName + " icons=" + usedIcons + " dots=" + usedDots + " missionY=" + my;
    }

    void OnDestroy()
    {
        if (mapMat != null) Destroy(mapMat);
        if (dotMat != null) Destroy(dotMat);
        if (dotTex != null) Destroy(dotTex);
    }

    public void Toggle()
    {
        IsUserVisible = !IsUserVisible;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab) || UICamera.inputHasFocus || PcPortTutorial.Blocking())
            return;
        if (!CanShowMinimap(out _))
            return;
        Toggle();
    }

    void LateUpdate()
    {
        HUDManager hud = HUDManager.instance;
        if (hud == null || hud.m_HotKeyManager == null || hud.m_InfoManager == null)
            return;
        if (builtHud != hud)
        {
            if (!TryBuild(hud))
                return;
        }
        SceneConfig cfg = null;
        bool show = IsUserVisible && CanShowMinimap(out cfg);
        if (root.activeSelf != show)
            root.SetActive(show);
        UpdateMissionOffset(show);
        if (!show)
            return;

        UpdateMinimap(cfg);
    }

    bool TryBuild(HUDManager hud)
    {
        HotKeyManager hotKeys = hud.m_HotKeyManager;
        if (hotKeys.m_Mission == null || hotKeys.m_Mission.transform.parent == null)
            return false;

        Shader minimapShader = Resources.Load<Shader>("PcPortMinimap") ?? Shader.Find("PcPort/Minimap");
        GameObject atlasGo = Resources.Load("MiniMapX/res/res/en/ENAtlas") as GameObject;
        UIAtlas atlas = atlasGo != null ? atlasGo.GetComponent<UIAtlas>() : null;
        if (minimapShader == null || atlas == null)
        {
            Debug.LogWarning("[PcPortMinimap] shader or MiniMapAtlas missing; keeping the original GPS bar");
            builtHud = hud;
            return false;
        }

        HideOldGps(hud);
        hk = hotKeys;
        missionOrigPos = hk.m_Mission.transform.localPosition;
        iconAtlas = atlas;

        if (mapMat == null)
        {
            mapMat = new Material(minimapShader) { hideFlags = HideFlags.DontSave };
            mapMat.renderQueue = 2998;
        }
        EnsureDotResources();

        root = NGUITools.AddChild(hk.m_Mission.transform.parent.gameObject); // Anchor-TopRight
        root.name = "PcPortMinimap";
        root.transform.localPosition = new Vector3(-Margin - Radius, -Margin - Radius, -5f);
        panel = root.AddComponent<UIPanel>();
        panel.depthPass = false;

        mapWidget = NGUITools.AddWidget<UITexture>(root);
        mapWidget.pivot = UIWidget.Pivot.Center;
        mapWidget.depth = 0;
        mapWidget.material = mapMat;
        mapWidget.color = Color.white;
        mapWidget.transform.localPosition = new Vector3(0f, 0f, 2f);
        mapWidget.transform.localScale = new Vector3(Diameter, Diameter, 1f);

        playerArrow = NGUITools.AddSprite(root, iconAtlas, "player0");
        playerArrow.pivot = UIWidget.Pivot.Center;
        playerArrow.depth = 50;
        playerArrow.transform.localPosition = new Vector3(0f, 0f, -2f);
        playerArrow.transform.localScale = new Vector3(16f, 20f, 1f);

        iconPool.Clear();
        dotPool.Clear();
        loadedSceneFile = "";
        mapTex = null;
        cachedPoiScene = null;
        builtHud = hud;
        return true;
    }

    void EnsureDotResources()
    {
        if (dotTex == null)
            dotTex = CreateDotTexture();
        if (dotMat == null)
        {
            Shader s = Shader.Find("Unlit/Transparent Colored") ?? Shader.Find("Unlit/Texture");
            dotMat = new Material(s) { hideFlags = HideFlags.DontSave };
            dotMat.mainTexture = dotTex;
            dotMat.renderQueue = 2999;
        }
    }

    static Texture2D CreateDotTexture()
    {
        const int sz = 24;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var px = new Color32[sz * sz];
        float c = (sz - 1) * 0.5f, maxR = sz * 0.45f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxR;
                float edge = 1f - Mathf.SmoothStep(0.86f, 1.04f, d);
                float fill = 1f - Mathf.SmoothStep(0.58f, 0.76f, d);
                px[y * sz + x] = new Color(fill, fill, fill, edge);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void HideOldGps(HUDManager hud)
    {
        if (hud.m_HotKeyManager.m_Map != null)
        {
            foreach (UIWidget w in hud.m_HotKeyManager.m_Map.GetComponentsInChildren<UIWidget>(true))
                w.enabled = false;
            foreach (Collider c in hud.m_HotKeyManager.m_Map.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }
        if (hud.m_InfoManager.m_MapState != null)
        {
            foreach (UIWidget w in hud.m_InfoManager.m_MapState.GetComponentsInChildren<UIWidget>(true))
                w.enabled = false;
            StateMap sm = hud.m_InfoManager.m_MapState.GetComponent<StateMap>();
            if (sm != null)
                sm.enabled = false;
        }
    }

    void UpdateMissionOffset(bool minimapShown)
    {
        if (hk == null || hk.m_Mission == null)
            return;
        float targetY = minimapShown ? (-Margin - Diameter - MissionGap - MissionHalfHeight) : missionOrigPos.y;
        Vector3 cur = hk.m_Mission.transform.localPosition;
        if (Mathf.Abs(cur.y - targetY) > 0.01f)
            hk.m_Mission.transform.localPosition = new Vector3(missionOrigPos.x, targetY, missionOrigPos.z);
    }

    bool CanShowMinimap(out SceneConfig cfg)
    {
        cfg = null;
        HUDManager hud = HUDManager.instance;
        if (hud == null || !hud.gameObject.activeInHierarchy || hk == null || hk.m_Map == null || !hk.m_Map.activeInHierarchy)
            return false;
        GameWorld world = GameApp.GetInstance().GetGameWorld();
        if (world == null || Arena.GetInstance().IsCurrentSceneArena() || world.IsVSScene())
            return false;
        LocalPlayer player = world.GetLocalPlayer();
        if (player == null || player.GetTransform() == null)
            return false;
        cfg = player.GetCurrentSceneConfig();
        if (cfg == null || string.IsNullOrEmpty(cfg.SceneFileName) || cfg.MiniMapSize.x <= 1f || cfg.MiniMapSize.y <= 1f)
            return false;
        if (Mathf.Abs(cfg.MapTopRight.x - cfg.MapTopLeft.x) < 1e-3f || Mathf.Abs(cfg.MapTopRight.z - cfg.MapBottomRight.z) < 1e-3f)
            return false;
        EnsureMapTexture(cfg.SceneFileName);
        return mapTex != null;
    }

    void EnsureMapTexture(string sceneFileName)
    {
        if (loadedSceneFile == sceneFileName)
            return;
        loadedSceneFile = sceneFileName;
        mapTex = null;
        GameObject prefab = Resources.Load("MiniMapX/Res/P_" + sceneFileName) as GameObject;
        if (prefab == null)
            return;
        UITexture ut = prefab.GetComponent<UITexture>();
        if (ut == null)
            return;
        // The material is already set in the P_* prefabs; read it without triggering CreatePanel on the asset
        mapTex = ut.material != null ? ut.material.mainTexture : ut.mainTexture;
        if (mapWidget != null && mapTex != null)
        {
            mapMat.mainTexture = mapTex;
            mapWidget.mainTexture = mapTex;
        }
    }

    void UpdateMinimap(SceneConfig cfg)
    {
        GameWorld world = GameApp.GetInstance().GetGameWorld();
        LocalPlayer player = world.GetLocalPlayer();
        Transform playerTr = player.GetTransform();
        Vector3 pos = playerTr.position;
        Vector3 fwd = playerTr.forward;

        float dxWorld = cfg.MapTopRight.x - cfg.MapTopLeft.x;
        float dzWorld = cfg.MapTopRight.z - cfg.MapBottomRight.z;
        float mapW = cfg.MiniMapSize.x;
        float mapH = cfg.MiniMapSize.y;
        float uPlayer = (pos.x - cfg.MapTopLeft.x) / dxWorld;
        float vPlayer = (pos.z - cfg.MapBottomRight.z) / dzWorld;

        Vector2 mapFwd = new Vector2(fwd.x / dxWorld * mapW, fwd.z / dzWorld * mapH);
        mapFwd = mapFwd.sqrMagnitude < 1e-8f ? new Vector2(0f, 1f) : mapFwd.normalized;
        float handedness = (dxWorld * dzWorld) >= 0f ? 1f : -1f;
        Vector2 mapRight = new Vector2(mapFwd.y * handedness, -mapFwd.x * handedness);

        mapMat.SetVector("_MapCenter", new Vector4(uPlayer, vPlayer, 0f, 0f));
        mapMat.SetVector("_MapRight", new Vector4(mapRight.x * MapRadiusPx / mapW, mapRight.y * MapRadiusPx / mapH, 0f, 0f));
        mapMat.SetVector("_MapUp", new Vector4(mapFwd.x * MapRadiusPx / mapW, mapFwd.y * MapRadiusPx / mapH, 0f, 0f));
        mapMat.SetFloat("_EdgeAA", 2.2f / Mathf.Max(1f, Diameter * Screen.height / 640f));

        usedIcons = 0;
        usedDots = 0;

        GameScene scene = GameApp.GetInstance().GetGameScene();
        if (scene != null)
        {
            if (cachedPoiScene != scene)
            {
                cachedPoiScene = scene;
                cachedGambles = GameObject.FindGameObjectsWithTag(MapController.TAG_GAMBLE);
                cachedUpgrades = GameObject.FindGameObjectsWithTag(MapController.TAG_UPGRADE_MACHINE);
            }
            PlaceFacilities(scene, cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
            PlacePortals(scene, cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
            if (world.IsCityScene())
                PlaceAssignableNpcs(scene, cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        }
        PlaceQuests(world, scene, pos, cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        PlaceUnits(UserStateHUD.GetInstance().GetEnemyList(), EnemyColor, 8.5f, 20, true,
            cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        PlaceUnits(UserStateHUD.GetInstance().GetRemotePlayerList(), AllyColor, 9f, 25, false,
            cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);

        for (int i = usedIcons; i < iconPool.Count; i++)
            iconPool[i].enabled = false;
        for (int i = usedDots; i < dotPool.Count; i++)
            dotPool[i].enabled = false;

        if (panel != null)
            panel.Refresh(); // update the icon vertices in the same frame as the shader so they do not shake while turning
    }

    void PlaceFacilities(GameScene scene, SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        GameObject store = scene.GetStore();
        if (store != null)
            PlaceIcon("store", 18f, 18f, 10, false, store.transform.position.x, store.transform.position.z,
                cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        PlaceArray(cachedGambles, "glamb", cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        PlaceArray(cachedUpgrades, "upgrade", cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
    }

    void PlaceArray(GameObject[] objs, string sprite, SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        if (objs == null)
            return;
        for (int i = 0; i < objs.Length; i++)
            if (objs[i] != null)
                PlaceIcon(sprite, 18f, 18f, 10, false, objs[i].transform.position.x, objs[i].transform.position.z,
                    cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
    }

    void PlacePortals(GameScene scene, SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        List<GameObject> portals = scene.GetPortal();
        if (portals == null)
            return;
        for (int i = 0; i < portals.Count; i++)
            if (portals[i] != null)
                PlaceIcon("entrance", 18f, 18f, 10, false, portals[i].transform.position.x, portals[i].transform.position.z,
                    cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
    }

    void PlaceAssignableNpcs(GameScene scene, SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        Dictionary<short, Npc> npcs = scene.GetNpc();
        if (npcs == null)
            return;
        foreach (KeyValuePair<short, Npc> kv in npcs)
        {
            Npc npc = kv.Value;
            if (npc == null || npc.GetTransform() == null)
                continue;
            QuestScript qs = npc.GetQuestScript();
            if (qs != null && qs.GetPromptType() == PromptType.Assignable)
                PlaceIcon("mainquest", 15f, 21f, 30, true, npc.GetTransform().position.x, npc.GetTransform().position.z,
                    cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
        }
    }

    void PlaceQuests(GameWorld world, GameScene scene, Vector3 playerPos, SceneConfig cfg,
        float dxWorld, float dzWorld, float mapW, float mapH, float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        Dictionary<string, QuestPoint> quests = UserStateHUD.GetInstance().GetQuestList();
        if (quests == null)
            return;
        QuestStateContainer container = GameApp.GetInstance().GetUserState().m_questStateContainer;
        QuestAttr attr = GameApp.GetInstance().GetGameMode().IsMultiPlayer()
            ? container.GetMarkQuestAttr()
            : container.GetCurrentQuestAttr();
        string sprite = attr == QuestAttr.MAIN_QUEST ? "mainquest" : "subquest";

        bool otherScene = false;
        foreach (KeyValuePair<string, QuestPoint> kv in quests)
        {
            QuestPoint qp = kv.Value;
            if (qp == null)
                continue;
            if (qp.m_siteId == world.CurrentSceneID)
            {
                List<QuestPoint.QuestPosition> positions = qp.GetPos();
                for (int i = 0; i < positions.Count; i++)
                    if (positions[i].m_state == QuestPointState.enable)
                        PlaceIcon(sprite, 15f, 21f, 32, true, positions[i].m_pos.x, positions[i].m_pos.y,
                            cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
            }
            else
            {
                otherScene = true;
            }
        }
        if (!otherScene || scene == null)
            return;
        List<GameObject> portals = scene.GetPortal();
        if (portals == null || portals.Count == 0)
            return;
        GameObject nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < portals.Count; i++)
        {
            GameObject p = portals[i];
            if (p == null)
                continue;
            float d = (p.transform.position - playerPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                nearest = p;
            }
        }
        if (nearest != null)
            PlaceIcon(sprite, 15f, 21f, 32, true, nearest.transform.position.x, nearest.transform.position.z,
                cfg, dxWorld, dzWorld, mapW, mapH, uPlayer, vPlayer, mapRight, mapFwd);
    }

    void PlaceUnits(Dictionary<string, UserStateHUD.GameUnitHUD> units, Color color, float size, int depth, bool scaleDanger,
        SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        if (units == null)
            return;
        foreach (KeyValuePair<string, UserStateHUD.GameUnitHUD> kv in units)
        {
            UserStateHUD.GameUnitHUD u = kv.Value;
            if (u == null || !u.IsActive || !u.IsAlive || u.Transform == null)
                continue;
            if (!TryProject(u.Position.x, u.Position.z, false, cfg, dxWorld, dzWorld, mapW, mapH,
                    uPlayer, vPlayer, mapRight, mapFwd, out Vector2 localPos))
                continue;
            UITexture dot = NextDot();
            dot.color = color;
            dot.depth = depth;
            float s = scaleDanger && u.IsDangerEnemy ? size * 1.25f : size;
            dot.transform.localScale = new Vector3(s, s, 1f);
            dot.transform.localPosition = new Vector3(localPos.x, localPos.y, -1f);
            dot.enabled = true;
        }
    }

    void PlaceIcon(string sprite, float w, float h, int depth, bool clampToRim, float wx, float wz,
        SceneConfig cfg, float dxWorld, float dzWorld, float mapW, float mapH,
        float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd)
    {
        if (!TryProject(wx, wz, clampToRim, cfg, dxWorld, dzWorld, mapW, mapH,
                uPlayer, vPlayer, mapRight, mapFwd, out Vector2 localPos))
            return;
        UISprite icon = NextIcon();
        if (icon.spriteName != sprite)
            icon.spriteName = sprite;
        icon.depth = depth;
        icon.transform.localScale = new Vector3(w, h, 1f);
        icon.transform.localPosition = new Vector3(localPos.x, localPos.y, -1.5f);
        icon.enabled = true;
    }

    static bool TryProject(float wx, float wz, bool clampToRim, SceneConfig cfg, float dxWorld, float dzWorld,
        float mapW, float mapH, float uPlayer, float vPlayer, Vector2 mapRight, Vector2 mapFwd, out Vector2 localPos)
    {
        float u = (wx - cfg.MapTopLeft.x) / dxWorld;
        float v = (wz - cfg.MapBottomRight.z) / dzWorld;
        float dx = (u - uPlayer) * mapW;
        float dy = (v - vPlayer) * mapH;
        float px = (dx * mapRight.x + dy * mapRight.y) / MapRadiusPx;
        float py = (dx * mapFwd.x + dy * mapFwd.y) / MapRadiusPx;
        float dist = Mathf.Sqrt(px * px + py * py);
        if (dist > InnerLimit)
        {
            if (!clampToRim)
            {
                localPos = Vector2.zero;
                return false;
            }
            float s = RimClamp / dist;
            px *= s;
            py *= s;
        }
        localPos = new Vector2(px * Radius, py * Radius);
        return true;
    }

    UISprite NextIcon()
    {
        if (usedIcons >= iconPool.Count)
        {
            UISprite s = NGUITools.AddSprite(root, iconAtlas, "mainquest");
            s.pivot = UIWidget.Pivot.Center;
            s.type = UISprite.Type.Simple;
            iconPool.Add(s);
        }
        return iconPool[usedIcons++];
    }

    UITexture NextDot()
    {
        if (usedDots >= dotPool.Count)
        {
            UITexture t = NGUITools.AddWidget<UITexture>(root);
            t.pivot = UIWidget.Pivot.Center;
            t.material = dotMat;
            dotPool.Add(t);
        }
        return dotPool[usedDots++];
    }
}
