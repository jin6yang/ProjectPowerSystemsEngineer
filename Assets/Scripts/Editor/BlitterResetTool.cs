using UnityEditor;
using UnityEngine.Rendering;

public class BlitterResetTool
{
    [MenuItem("Tools/Reset URP Blitter")]
    public static void ResetBlitter()
    {
        Blitter.Cleanup();
        UnityEngine.Debug.Log("Blitter has been forcefully cleaned up.");
    }
}
