using UnityEngine;

// shortcut: GUITexture stub (removed in Unity 2019.3) only so the Unity 4 code compiles;
// no scene or prefab of the game has this component, so GetComponent<GUITexture>() always returns null, as in the original.
public class GUITexture : MonoBehaviour
{
    public Color color;
    public Texture texture;
    public Rect pixelInset;
}
