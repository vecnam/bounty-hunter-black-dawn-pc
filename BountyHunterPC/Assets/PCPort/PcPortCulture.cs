using System;
using System.Globalization;
using System.Threading;
using UnityEngine;

// The game reads config numbers with float.Parse and no culture (GameConfig and 26 other places). On Android the culture was
// neutral; on a Windows set to e.g. pt-BR, "1.5" becomes 15 (slow-motion reloads). Forces the invariant culture process-wide.
public static class PcPortCulture
{
    // System culture before the switch: old saves stored dates as text in that format (e.g. "20/11/2026 23:34:00").
    static CultureInfo original = CultureInfo.CurrentCulture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        original = CultureInfo.CurrentCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    // Save dates: neutral format (new saves) or the system one (old saves); if neither works, now (the save is not lost).
    public static DateTime ParseDate(string s)
    {
        DateTime d;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ||
            DateTime.TryParse(s, original, DateTimeStyles.None, out d))
            return d;
        Debug.LogWarning("[PcPortCulture] unreadable date in the save: " + s);
        return DateTime.Now;
    }
}
