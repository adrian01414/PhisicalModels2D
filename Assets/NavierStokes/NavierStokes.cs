using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class FluidSimulator2D : MonoBehaviour
{
    [Header("Simulation Settings")]
    public int resolution = 128;
    [Range(0, 1)] public float damping = 0.98f;
    [Range(0, 0.1f)] public float diffusion = 0.01f;
    public float forceScale = 100f;
    public float inputRadius = 10f;

    private float[,] currentBuffer;
    private float[,] previousBuffer;
    private Texture2D texture;
    private Color[] pixels;
    private Image image;
    private RectTransform rectTransform;

    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        InitializeTexture();
        InitializeBuffers();
    }

    private void InitializeTexture()
    {
        texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        pixels = new Color[resolution * resolution];
        image.sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), Vector2.one * 0.5f);
    }

    private void InitializeBuffers()
    {
        currentBuffer = new float[resolution, resolution];
        previousBuffer = new float[resolution, resolution];
    }

    private void Update()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            AddInput(Input.GetMouseButton(0) ? 1f : -1f);
        }

        SimulateWave();
        UpdateTexture();
    }

    private void AddInput(float sign)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            Input.mousePosition,
            null,
            out localPoint);

        Vector2 uv = new Vector2(
            (localPoint.x / rectTransform.rect.width + 0.5f),
            (localPoint.y / rectTransform.rect.height + 0.5f));

        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        int centerX = (int)(uv.x * resolution);
        int centerY = (int)(uv.y * resolution);
        int radius = (int)(inputRadius * resolution / 100f);

        for (int y = Mathf.Max(0, centerY - radius); y < Mathf.Min(resolution, centerY + radius); y++)
        {
            for (int x = Mathf.Max(0, centerX - radius); x < Mathf.Min(resolution, centerX + radius); x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                if (dist < radius)
                {
                    float falloff = 1f - dist / radius;
                    previousBuffer[x, y] += sign * forceScale * falloff * Time.deltaTime * 100f;
                }
            }
        }
    }

    private void SimulateWave()
    {
        for (int y = 1; y < resolution - 1; y++)
        {
            for (int x = 1; x < resolution - 1; x++)
            {
                currentBuffer[x, y] = (
                    previousBuffer[x - 1, y] +
                    previousBuffer[x + 1, y] +
                    previousBuffer[x, y - 1] +
                    previousBuffer[x, y + 1]) / 2f - currentBuffer[x, y];

                currentBuffer[x, y] *= damping;

                float avg = (previousBuffer[x - 1, y] + previousBuffer[x + 1, y] +
                            previousBuffer[x, y - 1] + previousBuffer[x, y + 1]) * 0.25f;
                currentBuffer[x, y] = Mathf.Lerp(currentBuffer[x, y], avg, diffusion);
            }
        }

        float[,] temp = previousBuffer;
        previousBuffer = currentBuffer;
        currentBuffer = temp;
    }

    private void UpdateTexture()
    {
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float value = Mathf.Clamp01(previousBuffer[x, y] * 0.5f + 0.5f);
                Color color = Color.Lerp(Color.blue * 0.3f, Color.cyan, value);
                pixels[y * resolution + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
    }
}