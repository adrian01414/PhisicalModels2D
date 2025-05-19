using UnityEngine;
using UnityEngine.UI;

public class HeatSettingsController : MonoBehaviour
{
    public HeatController HeatController;
    public InputField TextureWidthField;
    public InputField TextureHeightField;
    public InputField BrushSizeField;

    [ContextMenu("Reset")]
    public void Reset()
    {
        HeatController.enabled = false;
        HeatController.textureWidth = int.Parse(TextureWidthField.text);
        HeatController.textureHeight = int.Parse(TextureHeightField.text);
        HeatController.brushSize = int.Parse(BrushSizeField.text);
        HeatController.enabled = true;
    }
}
