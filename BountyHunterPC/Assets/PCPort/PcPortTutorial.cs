using UnityEngine;

public static class PcPortTutorial
{
    // On the phone, a tutorial step's overlay blocks touches outside the highlighted button; keyboard shortcuts
    // respect the same block. Without it, opening the map mid-tutorial trapped the player in the menu (closing is locked).
    public static bool Blocking()
    {
        foreach (TutorialScript step in Object.FindObjectsByType<TutorialScript>(FindObjectsSortMode.None))
        {
            if (step.m_Context != null && step.m_Context.activeInHierarchy)
                return true;
        }
        return false;
    }
}
