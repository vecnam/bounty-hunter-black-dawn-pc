using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

// Harness: BountyHunter.exe -pcport-shot <scene|-|ui-msg|menu-N|...> <seconds> <file.png>
// Loads the scene ("-" = the game starts normally; "ui-msg" = opens CharacterUI + UIInput + MsgUI as a regression test),
// waits, saves the screenshot and quits the game.
public class PcPortShot : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-pcport-shot");
        if (i < 0 || i + 3 >= args.Length)
            return;
        GameObject go = new GameObject("PcPortShot");
        DontDestroyOnLoad(go);
        PcPortShot shot = go.AddComponent<PcPortShot>();
        shot.StartCoroutine(Run(args[i + 1], float.Parse(args[i + 2], CultureInfo.InvariantCulture), args[i + 3]));
    }

    static IEnumerator Run(string scene, float wait, string png)
    {
        if (scene == "ui-msg")
            yield return OpenUiMsgSample();
        else if (scene == "hud")
        {
            yield return WaitForHud(); // just enter the city from the save (to compare the city's look)
            LogHud();
        }
        else if (scene == "hud-tab")
        {
            yield return WaitForHud();
            if (PcPortMinimap.Instance != null)
            {
                Debug.Log("[PcPortMinimap] before Tab: " + PcPortMinimap.Instance.DebugSummary());
                PcPortMinimap.Instance.Toggle();
                yield return null;
                yield return null;
                Debug.Log("[PcPortMinimap] after Tab: " + PcPortMinimap.Instance.DebugSummary());
            }
        }
        else if (scene.StartsWith("menu-"))
        {
            // Enter the game from the save (normal flow) and open menu n like the keyboard shortcuts do (M = 1).
            yield return WaitForHud();
            Debug.Log("[PcPortShot] Show(" + scene.Substring(5) + ")");
            InGameMenuManager.GetInstance().Show(int.Parse(scene.Substring(5)));
        }
        else if (scene.StartsWith("reopen-"))
        {
            // Open menu N, close it like Esc does (CloseButton → Close) and open it again: the 2nd time it must be complete.
            yield return WaitForHud();
            int n = int.Parse(scene.Substring(7));
            InGameMenuManager.GetInstance().Show(n);
            yield return new WaitForSecondsRealtime(3f);
            LogWidgets("opened once");
            InGameMenuManager.GetInstance().Close();
            yield return new WaitForSecondsRealtime(3f);
            InGameMenuManager.GetInstance().Show(n);
        }
        else if (scene.StartsWith("fruit-"))
        {
            // Open the roulette (0 = mithril, 1 = gold) like the city machine does.
            yield return WaitForHud();
            UIFruitMachine.Show((GambleType)int.Parse(scene.Substring(6)));
        }
        else if (scene.StartsWith("portal-"))
        {
            // Enter the city from the save and go through the portal like UIPortal does (SetDest + end of the fade).
            yield return WaitForHud();
            PortalEnterTrigger.instance.SetDest(scene.Substring(7), 0);
            PortalEnterTrigger.instance.OnEffectsEnd(EffectsCamera.Type.FadeIn);
            // Jump (Space calls StartJump): measure the rise before waking enemies that could push the player.
            yield return new WaitForSecondsRealtime(wait);
            Transform body = GameApp.GetInstance().GetGameWorld().GetLocalPlayer().GetTransform();
            float y0 = body.position.y, top = y0;
            GameApp.GetInstance().GetGameWorld().GetLocalPlayer().StartJump();
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                top = Mathf.Max(top, body.position.y);
                yield return null;
            }
            Debug.Log("[PcPortShot] jump: rose " + (top - y0).ToString("0.00") + " m, ended " + (body.position.y - y0).ToString("0.00") + " m from the start");
            // Mission chests (blocks <scene>-1, -2...) loaded the way streaming loads them: does ChestScript.Start find the config?
            string lower = SceneManager.GetActiveScene().name.ToLower();
            for (int i = 1; i < 10; i++)
            {
                Object chest = PcPortBundle.Load("scenes/" + lower + "/android_prefab_" + lower + "-" + i).mainAsset;
                if (chest == null)
                    break;
                Object.Instantiate(chest);
            }
            // Wake the spawners as if the player had passed their triggers: tests the enemies on the NavMesh.
            foreach (BaseEnemySpawnScript spawner in Object.FindObjectsByType<BaseEnemySpawnScript>(FindObjectsSortMode.None))
                spawner.Activate();
        }
        else if (scene == "iap")
        {
            // Store through the mithril "+": on PC every pack is free. Then the gold "+" trades 100 mithril for gold.
            yield return WaitForHud();
            UIIAP.Show(UIIAP.Type.IAP);
            yield return new WaitForSecondsRealtime(2f);
            UIIAP iap = Object.FindFirstObjectByType<UIIAP>();
            LogMoney("store open, restore visible=" + iap.m_ButtonRestore.activeInHierarchy);
            foreach (GameObject b in iap.m_IapButtons)
            {
                b.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                LogMoney("clicked " + b.GetComponent<IapItem>().iapItemName + " (active=" + b.activeInHierarchy + ")");
            }
            UIIAP.Close();
            yield return null;
            UIIAP.Show(UIIAP.Type.Exchange);
            yield return new WaitForSecondsRealtime(2f);
            Object.FindFirstObjectByType<UIIAP>().m_ExchangeButtons[0].SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            LogMoney("traded with the 1st option");
        }
        else if (scene != "-")
            SceneManager.LoadScene(scene);
        yield return new WaitForSecondsRealtime(wait);
        LogSceneState();
        LogTextures();
        LogWidgets("final");
        foreach (ItemIcon icon in Object.FindObjectsByType<ItemIcon>(FindObjectsSortMode.None))
        {
            UISprite s = icon.item;
            Debug.Log("[PcPortShot] ItemIcon in " + (icon.transform.parent ? icon.transform.parent.name : "-") + ": sprite=" + s.spriteName
                + " atlas=" + (s.atlas ? s.atlas.name : "null") + " hasSprite=" + (s.atlas != null && s.atlas.GetSprite(s.spriteName) != null)
                + " material=" + (s.material ? s.material.name : "null") + " panel=" + (s.panel ? s.panel.name + " layer " + s.panel.gameObject.layer + " clip " + s.panel.clipping : "null")
                + " visible=" + s.isVisible + " alpha=" + s.finalAlpha + " pos=" + icon.transform.position + " scale=" + icon.transform.lossyScale + " size=" + s.transform.localScale
                + " layers icon/item/parent=" + icon.gameObject.layer + "/" + s.gameObject.layer + "/" + (icon.transform.parent ? icon.transform.parent.gameObject.layer : -1)
                + " panels=" + icon.GetComponentsInParent<UIPanel>(true).Length);
        }
        Time.timeScale = 0f; // freeze the scene: the frame without post-processing matches the one with it
        ScreenCapture.CaptureScreenshot(png);
        yield return new WaitForSecondsRealtime(2f); // the capture is written at the end of the frame
        // the same frame without the remaster's post-processing (PcPortPostFx), to judge its effect
        var layers = Object.FindObjectsByType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(FindObjectsSortMode.None);
        if (layers.Length > 0)
        {
            foreach (var layer in layers)
                layer.enabled = false;
            yield return null;
            ScreenCapture.CaptureScreenshot(png.Replace(".png", "-nopost.png"));
            yield return new WaitForSecondsRealtime(2f);
        }
        Application.Quit();
    }

    // Wait for the game to reach the HUD (phase 6): the time varies between runs (touch screen, save loading). With the
    // tutorials done the game stops at the title screen: then it goes PLAY → character screen → EnterGame of the current one.
    static IEnumerator WaitForHud()
    {
        float limit = Time.realtimeSinceStartup + 90f;
        bool entered = false;
        while (Time.realtimeSinceStartup < limit && !(HUDManager.instance != null && HUDManager.instance.gameObject.activeInHierarchy
            && GameApp.GetInstance().GetUIStateManager() != null && GameApp.GetInstance().GetUIStateManager().FrGetCurrentPhase() == 6))
        {
            if (!entered && UICharacter.instance != null)
                entered = UICharacter.instance.EnterGame();
            else if (!entered && Object.FindFirstObjectByType<UIStart>() != null && GameApp.GetInstance().GetUIStateManager().FrGetNextPhase() == 2)
                GameApp.GetInstance().GetUIStateManager().FrGoToPhase(20, false, false, false);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(3f);
    }

    static void LogMoney(string when)
    {
        Debug.Log("[PcPortShot] " + when + ": gold=" + GameApp.GetInstance().GetUserState().GetCash() + " mithril=" + GameApp.GetInstance().GetGlobalState().GetMithril());
    }

    // Render and NavMesh state when the screenshot is taken (to compare city vs mission in the log).
    static void LogSceneState()
    {
        Debug.Log("[PcPortShot] scene=" + SceneManager.GetActiveScene().name + " lightmaps=" + LightmapSettings.lightmaps.Length
            + " navmeshTris=" + UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length / 3);
        if (PcPortMinimap.Instance != null)
            Debug.Log("[PcPortMinimap] " + PcPortMinimap.Instance.DebugSummary());
        int onMesh = 0, moving = 0;
        UnityEngine.AI.NavMeshAgent[] agents = Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
        foreach (UnityEngine.AI.NavMeshAgent a in agents)
        {
            onMesh += a.isOnNavMesh ? 1 : 0;
            moving += a.velocity.sqrMagnitude > 0.01f ? 1 : 0;
        }
        Debug.Log("[PcPortShot] agents=" + agents.Length + " onNavMesh=" + onMesh + " moving=" + moving);
        foreach (ChestScript chest in Object.FindObjectsByType<ChestScript>(FindObjectsSortMode.None))
            Debug.Log("[PcPortShot] chest " + chest.name + " config=" + GameConfig.GetInstance().chestDropConfigName.ContainsKey(chest.name));
        foreach (Camera c in Camera.allCameras)
            Debug.Log("[PcPortShot] camera " + c.name + " depth=" + c.depth + " clear=" + c.clearFlags + " far=" + c.farClipPlane
                + " mask=" + c.cullingMask + " pos=" + c.transform.position);
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (r.name.StartsWith("ground") || r.name.StartsWith("item") || r.name == "sky" || r.name == "Metal_Parts")
                Debug.Log("[PcPortShot] " + r.name + " enabled=" + r.enabled + " visible=" + r.isVisible + " lm=" + r.lightmapIndex
                    + " st=" + r.lightmapScaleOffset + " static=" + r.isPartOfStaticBatch + " layer=" + r.gameObject.layer + " bounds=" + r.bounds
                    + " shader=" + (r.sharedMaterial ? r.sharedMaterial.shader.name : "null"));
    }

    // Tree of the HUD buttons (HotKeyManager): path, active, position, scale, widget (sprite/text/depth) and anchor,
    // plus the parent chain up to the root. The reference for layout changes (PcPortHud).
    static void LogHud()
    {
        HotKeyManager hk = HUDManager.instance.m_HotKeyManager;
        UIRoot root = hk.GetComponentInParent<UIRoot>();
        Debug.Log("[PcPortHud] screen=" + Screen.width + "x" + Screen.height + " root=" + (root ? root.name + " height=" + root.manualHeight + " " + root.scalingStyle : "-"));
        var buttons = new (string, GameObject)[] { ("aim", hk.m_Aim), ("melee", hk.m_Melee), ("grenade", hk.m_Grenade),
            ("swap", hk.m_SwapOrReload), ("skill1", hk.m_Skill1), ("skill2", hk.m_Skill2), ("return", hk.m_Return),
            ("weaponlist", hk.m_WeaponList), ("interact", hk.m_Interact), ("pill", hk.m_Pill) };
        foreach ((string name, GameObject go) in buttons)
        {
            if (go == null)
            {
                Debug.Log("[PcPortHud] " + name + " = null");
                continue;
            }
            string chain = "";
            for (Transform p = go.transform.parent; p != null; p = p.parent)
                chain = p.name + (p.GetComponent<UIAnchor>() ? "<" + p.GetComponent<UIAnchor>().side + ">" : "") + (p.GetComponent<UIPanel>() ? "[panel]" : "") + "/" + chain;
            Debug.Log("[PcPortHud] " + name + " parents=" + chain + " activeInHierarchy=" + go.activeInHierarchy);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                string path = t.name;
                for (Transform p = t.parent; p != null && p != go.transform.parent; p = p.parent)
                    path = p.name + "/" + path;
                UIWidget w = t.GetComponent<UIWidget>();
                string info = w == null ? "" : " " + w.GetType().Name + (w is UISprite s ? " sprite=" + s.spriteName + " atlas=" + (s.atlas ? s.atlas.name : "-") : "")
                    + (w is UILabel l ? " text='" + l.text + "'" : "") + " depth=" + w.depth + " color=" + w.color + " pivot=" + w.pivot;
                UIAnchor a = t.GetComponent<UIAnchor>();
                Debug.Log("[PcPortHud]   " + path + " active=" + t.gameObject.activeSelf + " pos=" + t.localPosition + " scale=" + t.localScale + info
                    + (a ? " anchor=" + a.side + " rel=" + a.relativeOffset : "") + (t.GetComponent<Collider>() ? " collider" : ""));
            }
        }
    }

    // Textures of the active 3D objects, without the NGUI UI (texture diagnostics).
    static void LogTextures()
    {
        HashSet<Texture> seen = new HashSet<Texture>();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || r.GetComponent<UIDrawCall>() != null)
                continue;
            foreach (Material m in r.sharedMaterials)
                if (m != null)
                    foreach (string p in m.GetTexturePropertyNames())
                    {
                        Texture t = m.GetTexture(p);
                        if (t != null && seen.Add(t))
                            Debug.Log("[PcPortShot] texture " + t.name + " " + t.width + "x" + t.height + " on " + r.name);
                    }
        }
    }

    // Active NGUI widgets without a material or texture (they draw invisible), per root object.
    static void LogWidgets(string when)
    {
        var total = new System.Collections.Generic.SortedDictionary<string, int>();
        var broken = new System.Collections.Generic.Dictionary<string, int>();
        var atlases = new System.Collections.Generic.HashSet<string>();
        foreach (UIWidget w in Object.FindObjectsByType<UIWidget>(FindObjectsSortMode.None))
        {
            string root = w.transform.root.name;
            total[root] = (total.TryGetValue(root, out int t) ? t : 0) + 1;
            if (w.material == null || w.mainTexture == null)
            {
                broken[root] = (broken.TryGetValue(root, out int b) ? b : 0) + 1;
                UISprite sprite = w as UISprite;
                atlases.Add(sprite != null && sprite.atlas != null ? sprite.atlas.name : w.GetType().Name);
            }
        }
        foreach (var kv in total)
            Debug.Log("[PcPortShot] " + when + ": " + kv.Key + " " + kv.Value + " widgets, " + (broken.TryGetValue(kv.Key, out int b) ? b : 0) + " without material/texture");
        Debug.Log("[PcPortShot] " + when + ": atlases without material: " + string.Join(", ", atlases));
    }

    static IEnumerator OpenUiMsgSample()
    {
        yield return new WaitForSecondsRealtime(3.5f);
        var charPrefab = ResourceLoad.GetInstance().LoadUI("MainMenuX", "CharacterUI");
        if (charPrefab != null)
        {
            var charGo = Object.Instantiate(charPrefab) as GameObject;
            yield return null;
            if (UICharacter.instance != null)
                UICharacter.instance.ShowMenuCreate();
            yield return new WaitForSecondsRealtime(1.5f);

            var inp = charGo.GetComponentInChildren<UIInput>(true);
            if (inp != null)
            {
                UICamera.selectedObject = inp.gameObject;
                inp.SendMessage("OnInput", "Hero1", SendMessageOptions.DontRequireReceiver);
            }
        }
        if (UIMsgBox.instance != null)
        {
            string msg = LocalizationManager.GetInstance().GetString("MSG_CREATE_ROLE_SUCCESS");
            UIMsgBox.instance.ShowMessage(UIMsg.EMPTY, msg, 3, 2);
        }
    }
}
