using UnityEngine;

// shortcut: GUIText stub (removed in Unity 2019.3) only so the Unity 4 code compiles;
// no scene or prefab of the game has this component, so GetComponent<GUIText>() always returns null, as in the original.
public class GUIText : MonoBehaviour
{
    public Color color;
    public string text;
    public Material material;
    public Font font;
    public int fontSize;
}
