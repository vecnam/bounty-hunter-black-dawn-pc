using UnityEngine;

// PC cursor: locked only in combat (FirstPersonCameraScript mouse-look); free in menus, on screens without a HUD
// and while left Alt is held (to click the HUD, tutorial and NPCs). Esc in combat opens the options menu.
public class PcPortCursor : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        GameObject go = new GameObject("PcPortCursor");
        DontDestroyOnLoad(go);
        go.AddComponent<PcPortCursor>();
    }

    // LateUpdate: runs after every Update, so the Esc that opens the menu is not read again by the new menu's CloseButton.
    void LateUpdate()
    {
        bool inCombat = false;
        if (HUDManager.instance != null && HUDManager.instance.gameObject.activeInHierarchy && GameApp.GetInstance().GetGameWorld() != null)
        {
            LocalPlayer player = GameApp.GetInstance().GetGameWorld().GetLocalPlayer();
            inCombat = player != null && !player.InputController.Block;
        }
        if (inCombat && Input.GetKeyDown(KeyCode.Escape) && !PcPortTutorial.Blocking())
        {
            InGameMenuManager.GetInstance().Show(5); // same menu as the O key (options)
            inCombat = false;
        }
        bool locked = inCombat && !Input.GetKey(KeyCode.LeftAlt);
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
