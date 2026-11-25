using UnityEngine;

// Unity 4.1 had no built-in "UI" layer (layer 5 was only named in 4.6): LayerMask.NameToLayer("UI") returned the game's
// layer 27, the one of the UI cameras. Unity 6 returns 5, which no camera draws: the roulette icons and the character model
// in the menus disappeared. Here the game's layer (8 to 31) wins over the built-in one with the same name.
public static class PcPortLayer
{
    public static int NameToLayer(string name)
    {
        for (int i = 8; i < 32; i++)
        {
            if (LayerMask.LayerToName(i) == name)
                return i;
        }
        return LayerMask.NameToLayer(name);
    }
}
