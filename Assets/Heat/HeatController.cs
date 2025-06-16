using UnityEngine;
using UnityEngine.UI;

public class HeatController : MonoBehaviour
{
    [Header("Texture Settings")]
    public int textureWidth = 512;
    public int textureHeight = 512;

    [Header("Heat Settings")]
    [GradientUsage(true)]
    public Gradient Gradient;
    public float brushSize = 20f;
    [Range(0f, 1f)] public float heatIntensity = 0.1f;
    [Range(0f, 0.1f)] public float diffusionRate = 0.05f;
    [Range(0f, 1f)] public float coolingRate = 0.01f;

    private RawImage heatImage;
    private RenderTexture heatRenderTexture;
    private RenderTexture tempRenderTexture;
    public ComputeShader computeShader;
    private int diffusionKernel;
    private int addHeatKernel;
    private Texture2D displayTexture;

    void OnEnable()
    {
        heatImage = GetComponent<RawImage>();

        heatRenderTexture = CreateRenderTexture(textureWidth, textureHeight);
        tempRenderTexture = CreateRenderTexture(textureWidth, textureHeight);

        diffusionKernel = computeShader.FindKernel("HeatDiffusion");
        addHeatKernel = computeShader.FindKernel("AddHeat");

        displayTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        heatImage.texture = displayTexture;

        ClearHeatMap();
    }

    RenderTexture CreateRenderTexture(int width, int height)
    {
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat);
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    Vector2 mousePos;
    Vector2 texturePos;
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            mousePos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            texturePos = ConvertScreenToTextureCoords(mousePos);
            AddHeat(texturePos.x, texturePos.y);
        }

        UpdateHeatMap();
        UpdateDisplayTexture();
    }

    Vector2 ConvertScreenToTextureCoords(Vector2 screenPos)
    {
        RectTransform rect = heatImage.rectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out localPoint);

        Vector2 normalized = new Vector2(
            (localPoint.x + rect.rect.width * 0.5f) / rect.rect.width,
            (localPoint.y + rect.rect.height * 0.5f) / rect.rect.height
        );

        return new Vector2(
            Mathf.Clamp(normalized.x * textureWidth, 0, textureWidth - 1),
            Mathf.Clamp(normalized.y * textureHeight, 0, textureHeight - 1)
        );
    }

    void AddHeat(float x, float y)
    {
        computeShader.SetVector("BrushPosition", new Vector2(x, y));
        computeShader.SetFloat("BrushSize", brushSize);
        computeShader.SetFloat("HeatIntensity", heatIntensity);
        computeShader.SetTexture(addHeatKernel, "HeatOutput", heatRenderTexture);

        computeShader.Dispatch(addHeatKernel,
            Mathf.CeilToInt(textureWidth / 8f),
            Mathf.CeilToInt(textureHeight / 8f),
            1);
    }

    void UpdateHeatMap()
    {
        computeShader.SetFloat("DiffusionRate", diffusionRate);
        computeShader.SetFloat("CoolingRate", coolingRate);
        computeShader.SetTexture(diffusionKernel, "HeatInput", heatRenderTexture);
        computeShader.SetTexture(diffusionKernel, "HeatOutput", tempRenderTexture);

        computeShader.Dispatch(diffusionKernel,
            Mathf.CeilToInt(textureWidth / 8f),
            Mathf.CeilToInt(textureHeight / 8f),
            1);

        Graphics.Blit(tempRenderTexture, heatRenderTexture);
    }

    void UpdateDisplayTexture()
    {
        RenderTexture.active = heatRenderTexture;
        displayTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);

        Color[] pixels = displayTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float heatValue = pixels[i].r;
            pixels[i] = Gradient.Evaluate(heatValue);
        }
        displayTexture.SetPixels(pixels);
        displayTexture.Apply();

        RenderTexture.active = null;
    }

    void ClearHeatMap()
    {
        RenderTexture.active = heatRenderTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = null;
    }

    void OnDestroy()
    {
        heatRenderTexture.Release();
        tempRenderTexture.Release();
    }
}