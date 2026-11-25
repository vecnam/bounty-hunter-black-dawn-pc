using UnityEngine;

// The game starts full screen at the monitor's resolution; Alt+Enter switches to a window during the session
// (allowFullscreenSwitch). Unity keeps the last screen mode in the preferences, and harness runs
// (-screen-fullscreen 0 -screen-width 1280) left the player's game in a 1280x720 window.
// shortcut: every start goes back to full screen (ignores the window chosen last time); to keep a window, store the
// player's choice in PlayerPrefs.
public static class PcPortScreen
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-pcport-shot") >= 0)
            return;
        Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
    }
}
