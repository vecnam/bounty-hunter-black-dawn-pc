using System.Collections.Generic;
using UnityEngine;

// PC HUD: no touch buttons for aim and melee (right mouse button and V still work), and weapon, grenades and powers in
// a rectangular panel in the bottom right corner, like PC shooters.
// The widgets are the game's own, and the game keeps updating ammo, grenades and power cooldowns: here they only
// move and lose their hexagonal frames. For aim and melee only the sprite is hidden (as in Joystick.Start): the game
// turns those buttons on and off and tutorial steps may wait for them.
public class PcPortHud : MonoBehaviour
{
    // units of the HUD's UIRoot (FixedSize, height 640)
    const float Margin = 14f, Height = 68f, SmallWidth = 56f, WeaponWidth = 150f, Cap = 13f;
    static readonly Color Teal = new Color(0.64f, 1f, 0.91f); // the [a3ffe8] of the HUD texts
    static readonly Color Dark = new Color(0.02f, 0.045f, 0.07f);

    HotKeyManager hk;
    HotKeyManager failed;
    GameObject panel;
    UITexture back;
    Transform slotSkill2, slotSkill1, slotGrenade, slotWeapon;
    Transform skill1Button, skill2Button, weaponIcon, ammo, grenadeIcon, grenadeCount;
    UILabel keySkill2, keySkill1, keyGrenade;
    string layout = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("PcPortHud");
        DontDestroyOnLoad(go);
        go.AddComponent<PcPortHud>();
    }

    void LateUpdate()
    {
        HUDManager hud = HUDManager.instance;
        if (hud == null || hud.m_HotKeyManager == null || hud.m_HotKeyManager == failed)
            return;
        if (hk != hud.m_HotKeyManager)
        {
            try
            {
                Build(hud.m_HotKeyManager);
            }
            catch (System.Exception e)
            {
                failed = hud.m_HotKeyManager; // HUD with another layout (e.g. VS): left as the game built it
                hk = null;
                Debug.LogWarning("[PcPortHud] HUD not recognized, keeping the original: " + e.Message);
                return;
            }
        }
        Arrange();
    }

    void Build(HotKeyManager hotKeys)
    {
        layout = "";
        Hide(hotKeys.m_Aim.transform);
        Hide(hotKeys.m_Melee.transform);
        panel = NGUITools.AddChild(hotKeys.m_Grenade.transform.parent.gameObject); // Anchor-BottomRight
        panel.name = "PcPortHudPanel";
        back = NGUITools.AddWidget<UITexture>(panel);
        back.shader = Shader.Find("Unlit/Transparent Colored");
        back.color = Color.white;

        skill2Button = hotKeys.m_Skill2.transform.Find("Button");
        skill1Button = hotKeys.m_Skill1.transform.Find("Button");
        Hide(skill2Button.Find("Background"));
        Hide(skill1Button.Find("Background"));
        Hide(hotKeys.m_Grenade.transform.Find("Background"));
        Hide(hotKeys.m_SwapOrReload.transform.Find("Sprite (weapon_n)"));
        // the power buttons sit at (-50, 40) and (-50, 120) inside their root: the root is shifted so the button lands centered
        slotSkill2 = Slot("Skill2", hotKeys.m_Skill2.transform, new Vector3(50f, -120f, -3f), 0.6f);
        slotSkill1 = Slot("Skill1", hotKeys.m_Skill1.transform, new Vector3(50f, -40f, -3f), 0.6f);
        slotGrenade = Slot("Grenade", hotKeys.m_Grenade.transform, Vector3.zero, 1f);
        slotWeapon = Slot("Weapon", hotKeys.m_SwapOrReload.transform, Vector3.zero, 1.35f);

        grenadeIcon = hotKeys.m_Grenade.transform.Find("Sprite (target_n)");
        grenadeIcon.localScale = new Vector3(30f, 35f, 1f);
        UILabel count = hotKeys.m_Grenade.GetComponentInChildren<UILabel>(true);
        count.pivot = UIWidget.Pivot.Left;
        count.transform.localScale = new Vector3(18f, 18f, 1f);
        grenadeCount = count.transform;
        weaponIcon = hotKeys.m_SwapOrReload.transform.Find("Icon");
        UILabel bullets = hotKeys.m_SwapOrReload.GetComponentInChildren<UILabel>(true);
        bullets.transform.localScale = new Vector3(19f, 19f, 1f);
        ammo = bullets.transform;

        keySkill2 = Key("C", count.font);
        keySkill1 = Key("F", count.font);
        keyGrenade = Key("G", count.font);
        hk = hotKeys;
    }

    // Positions the game may move (weapon swap, weapon list tween) and the layout when what is shown changes.
    void Arrange()
    {
        bool weapon = hk.m_SwapOrReload.activeSelf, grenade = hk.m_Grenade.activeSelf;
        bool skill1 = hk.m_Skill1.activeSelf && skill1Button.gameObject.activeSelf;
        bool skill2 = hk.m_Skill2.activeSelf && skill2Button.gameObject.activeSelf;
        string now = weapon + "" + grenade + skill1 + skill2 + Screen.height;
        if (now != layout)
        {
            layout = now;
            Relayout(weapon, grenade, skill1, skill2);
        }
        hk.m_SwapOrReload.transform.localPosition = Vector3.zero;
        weaponIcon.localPosition = new Vector3(-6f, 10f, -8f);
        ammo.localPosition = new Vector3(50f, -12.5f, -20f);
        grenadeIcon.localPosition = new Vector3(-9f, 3f, 0f);
        grenadeCount.localPosition = new Vector3(5f, -15f, -20f);
    }

    void Relayout(bool weapon, bool grenade, bool skill1, bool skill2)
    {
        // left to right: power 2, power 1, grenade, weapon (only what is visible)
        var sections = new List<(Transform slot, UILabel key, float width)>();
        if (skill2) sections.Add((slotSkill2, keySkill2, SmallWidth));
        if (skill1) sections.Add((slotSkill1, keySkill1, SmallWidth));
        if (grenade) sections.Add((slotGrenade, keyGrenade, SmallWidth));
        if (weapon) sections.Add((slotWeapon, null, WeaponWidth));
        keySkill2.enabled = skill2;
        keySkill1.enabled = skill1;
        keyGrenade.enabled = grenade;
        back.enabled = sections.Count > 0;
        if (sections.Count == 0)
            return;

        float width = 0f;
        foreach (var s in sections)
            width += s.width;
        float left = -Margin - width, middle = Margin + Height / 2f, x = 0f;
        var separators = new List<float>();
        var caps = new List<Vector2>();
        foreach (var s in sections)
        {
            bool small = s.width == SmallWidth;
            s.slot.localPosition = new Vector3(left + x + s.width / 2f + (small ? 3f : 0f), middle - (small ? 3f : 0f), 0f);
            if (s.key != null)
            {
                var cap = new Vector2(x + 3f + Cap / 2f, Height - 3f - Cap / 2f); // top left corner
                caps.Add(cap);
                s.key.transform.localPosition = new Vector3(left + cap.x, Margin + cap.y + 0.5f, -30f);
            }
            x += s.width;
            if (x < width)
                separators.Add(x);
        }
        back.transform.localPosition = new Vector3(left + width / 2f, middle, 10f);
        back.transform.localScale = new Vector3(width, Height, 1f);
        Texture old = back.mainTexture;
        back.mainTexture = Paint(width, separators, caps);
        if (old != null)
            Destroy(old);
    }

    // Panel background drawn in screen pixels: translucent dark that fades out to the left, a turquoise line on top,
    // dividers and the shortcut "keys".
    static Texture2D Paint(float width, List<float> separators, List<Vector2> caps)
    {
        float ppu = Screen.height / 640f;
        int tw = Mathf.Max(8, Mathf.CeilToInt(width * ppu)), th = Mathf.Max(8, Mathf.CeilToInt(Height * ppu));
        var px = new Color32[tw * th];
        for (int y = 0; y < th; y++)
            for (int x = 0; x < tw; x++)
            {
                float u = x / ppu, v = y / ppu; // in UIRoot units, (0,0) at the bottom left
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 10f)); // only the left edge fades
                Color c = new Color(Dark.r, Dark.g, Dark.b, 0.8f * fade * (0.88f + 0.12f * (1f - v / Height)));
                if (v > Height - 1.6f)
                    c = Over(c, Teal, 0.9f * fade);
                foreach (float s in separators)
                    if (Mathf.Abs(u - s) < 0.55f && v > Height * 0.18f && v < Height * 0.82f)
                        c = Over(c, Teal, 0.22f);
                foreach (Vector2 cap in caps)
                {
                    float d = RoundedBox(new Vector2(u, v) - cap, Cap / 2f, 2.6f);
                    if (d < 0f)
                        c = Over(c, Dark, 0.45f);
                    c = Over(c, Teal, 0.85f * Mathf.Clamp01(1f - Mathf.Abs(d) * ppu / 1.4f)); // outline of about 1 screen pixel
                }
                px[y * tw + x] = c;
            }
        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // signed distance to a square of half-width h with corner radius r
    static float RoundedBox(Vector2 p, float h, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(h - r, h - r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    // "src over dst" with alpha a
    static Color Over(Color dst, Color src, float a)
    {
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f)
            return dst;
        Color rgb = (src * a + dst * dst.a * (1f - a)) / outA;
        return new Color(rgb.r, rgb.g, rgb.b, outA);
    }

    Transform Slot(string name, Transform content, Vector3 offset, float scale)
    {
        GameObject slot = NGUITools.AddChild(panel);
        slot.name = name;
        slot.transform.localScale = new Vector3(scale, scale, 1f);
        content.SetParent(slot.transform, false);
        content.localPosition = offset;
        NGUITools.MarkParentAsChanged(content.gameObject);
        return slot.transform;
    }

    UILabel Key(string letter, UIFont font)
    {
        UILabel label = NGUITools.AddWidget<UILabel>(panel);
        label.font = font;
        label.text = letter;
        label.pivot = UIWidget.Pivot.Center;
        label.color = Teal;
        label.transform.localScale = new Vector3(10f, 10f, 1f);
        return label;
    }

    static void Hide(Transform t)
    {
        if (t == null)
            return;
        foreach (UIWidget w in t.GetComponentsInChildren<UIWidget>(true))
            w.enabled = false;
    }
}
