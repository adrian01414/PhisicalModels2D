using UnityEngine;
using UnityEngine.UI;

public class NavierStokesSimulation : MonoBehaviour
{
    [Header("Simulation Parameters")]
    public float viscosity = 0.001f;
    public float diffusion = 0.001f;
    public float force = 10f;
    public float source = 1f;
    public float damping = 0.99f;
    public int iterations = 10;

    [Header("Interaction")]
    public float mouseForce = 10f;
    public float mouseRadius = 10f;
    public Color dyeColor = Color.blue;

    [Header("References")]
    public ComputeShader navierStokesCS;
    public RawImage targetImage;

    private RenderTexture velocityRT;
    private RenderTexture prevVelocityRT;
    private RenderTexture densityRT;
    private RenderTexture prevDensityRT;
    private RenderTexture pressureRT;
    private RenderTexture divergenceRT;
    private RenderTexture obstaclesRT;

    private int textureWidth = 256;
    private int textureHeight = 256;

    private int advectKernel;
    private int diffuseKernel;
    private int projectKernel;
    private int addDensityKernel;
    private int addVelocityKernel;

    private Vector2 previousMousePos;

    void Start()
    {
        InitializeTextures();
        FindKernels();
        targetImage.texture = densityRT;
    }

    void InitializeTextures()
    {
        velocityRT = CreateRenderTexture(RenderTextureFormat.ARGBFloat);
        prevVelocityRT = CreateRenderTexture(RenderTextureFormat.ARGBFloat);
        densityRT = CreateRenderTexture(RenderTextureFormat.RFloat);
        prevDensityRT = CreateRenderTexture(RenderTextureFormat.RFloat);
        pressureRT = CreateRenderTexture(RenderTextureFormat.RFloat);
        divergenceRT = CreateRenderTexture(RenderTextureFormat.RFloat);
        obstaclesRT = CreateRenderTexture(RenderTextureFormat.RFloat);
    }

    RenderTexture CreateRenderTexture(RenderTextureFormat format)
    {
        var rt = new RenderTexture(textureWidth, textureHeight, 0, format)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();
        return rt;
    }

    void FindKernels()
    {
        advectKernel = navierStokesCS.FindKernel("Advect");
        diffuseKernel = navierStokesCS.FindKernel("Diffuse");
        projectKernel = navierStokesCS.FindKernel("Project");
        addDensityKernel = navierStokesCS.FindKernel("AddDensity");
        addVelocityKernel = navierStokesCS.FindKernel("AddVelocity");
    }

    void Update()
    {
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            AddMouseInteraction();
        }

        SimulateStep(Time.deltaTime);
    }

    void AddMouseInteraction()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetImage.rectTransform,
            Input.mousePosition,
            null,
            out Vector2 localPoint);

        Vector2 uv = new Vector2(
            (localPoint.x + targetImage.rectTransform.rect.width * 0.5f) / targetImage.rectTransform.rect.width,
            (localPoint.y + targetImage.rectTransform.rect.height * 0.5f) / targetImage.rectTransform.rect.height);

        if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return;

        Vector2 mousePos = new Vector2(uv.x * textureWidth, uv.y * textureHeight);
        Vector2 mouseDelta = mousePos - previousMousePos;

        if (Input.GetMouseButton(0))
        {
            // Add velocity
            navierStokesCS.SetFloat("Force", mouseForce);
            navierStokesCS.SetFloat("Source", 0);
            navierStokesCS.SetTexture(addVelocityKernel, "Velocity", velocityRT);
            navierStokesCS.SetVector("MousePos", mousePos);
            navierStokesCS.SetFloat("MouseRadius", mouseRadius);
            DispatchMouseInteraction(addVelocityKernel, mousePos);
        }

        if (Input.GetMouseButton(1))
        {
            // Add density
            navierStokesCS.SetFloat("Source", source);
            navierStokesCS.SetTexture(addDensityKernel, "Density", densityRT);
            navierStokesCS.SetVector("MousePos", mousePos);
            navierStokesCS.SetFloat("MouseRadius", mouseRadius);
            DispatchMouseInteraction(addDensityKernel, mousePos);
        }

        previousMousePos = mousePos;
    }

    void DispatchMouseInteraction(int kernel, Vector2 mousePos)
    {
        int threadGroupsX = Mathf.CeilToInt(mouseRadius * 2 / 8);
        int threadGroupsY = Mathf.CeilToInt(mouseRadius * 2 / 8);
        navierStokesCS.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
    }

    void SimulateStep(float dt)
    {
        SetCommonParameters(dt);

        // Advect
        Swap(ref velocityRT, ref prevVelocityRT);
        Swap(ref densityRT, ref prevDensityRT);
        navierStokesCS.SetTexture(advectKernel, "Velocity", velocityRT);
        navierStokesCS.SetTexture(advectKernel, "PrevVelocity", prevVelocityRT);
        navierStokesCS.SetTexture(advectKernel, "Density", densityRT);
        navierStokesCS.SetTexture(advectKernel, "PrevDensity", prevDensityRT);
        Dispatch(advectKernel);

        // Diffuse
        navierStokesCS.SetTexture(diffuseKernel, "Velocity", velocityRT);
        navierStokesCS.SetTexture(diffuseKernel, "Density", densityRT);
        Dispatch(diffuseKernel);

        // Project
        for (int i = 0; i < iterations; i++)
        {
            navierStokesCS.SetTexture(projectKernel, "Velocity", velocityRT);
            navierStokesCS.SetTexture(projectKernel, "Pressure", pressureRT);
            navierStokesCS.SetTexture(projectKernel, "Divergence", divergenceRT);
            Dispatch(projectKernel);
        }

        // Apply damping
        ApplyDamping();
    }

    void SetCommonParameters(float dt)
    {
        navierStokesCS.SetFloat("DeltaTime", dt);
        navierStokesCS.SetFloat("Viscosity", viscosity);
        navierStokesCS.SetFloat("Diffusion", diffusion);
        navierStokesCS.SetInt("TextureWidth", textureWidth);
        navierStokesCS.SetInt("TextureHeight", textureHeight);
    }

    void Dispatch(int kernel)
    {
        int threadGroupsX = Mathf.CeilToInt(textureWidth / 8f);
        int threadGroupsY = Mathf.CeilToInt(textureHeight / 8f);
        navierStokesCS.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
    }

    void ApplyDamping()
    {
        Graphics.Blit(velocityRT, prevVelocityRT);
        Graphics.Blit(densityRT, prevDensityRT);

        var mat = new Material(Shader.Find("Hidden/BlitScale"));
        mat.SetFloat("_Scale", damping);
        Graphics.Blit(prevVelocityRT, velocityRT, mat);
        Graphics.Blit(prevDensityRT, densityRT, mat);

        Destroy(mat);
    }

    void Swap(ref RenderTexture a, ref RenderTexture b)
    {
        RenderTexture temp = a;
        a = b;
        b = temp;
    }

    void OnDestroy()
    {
        ReleaseTextures();
    }

    void ReleaseTextures()
    {
        velocityRT.Release();
        prevVelocityRT.Release();
        densityRT.Release();
        prevDensityRT.Release();
        pressureRT.Release();
        divergenceRT.Release();
        obstaclesRT.Release();
    }
}