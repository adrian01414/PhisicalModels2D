using UnityEngine;

public class NavierStokesGPU : MonoBehaviour
{
    [Header("Simulation Settings")]
    public int resolution = 128;
    [Range(0, 0.01f)] public float viscosity = 0.0001f;
    [Range(0, 0.01f)] public float diffusion = 0.0001f;
    [Range(0, 50f)] public float force = 10f;
    [Range(1, 20f)] public float sourceRadius = 10f;

    [Header("Visualization")]
    public Gradient colorGradient;
    public ComputeShader computeShader;

    private ComputeBuffer density;
    private ComputeBuffer densityPrev;
    private ComputeBuffer velocityX;
    private ComputeBuffer velocityY;
    private ComputeBuffer velocityXPrev;
    private ComputeBuffer velocityYPrev;
    private ComputeBuffer vorticity;
    private ComputeBuffer curl;

    private Texture2D texture;
    private Renderer rend;
    private float[] densityArray;

    private int diffuseKernel;
    private int linearSolveKernel;
    private int projectKernel1;
    private int projectKernel2;
    private int advectKernel;
    private int setBoundaryKernel;
    private int addSourceKernel;

    private void Start()
    {
        InitializeBuffers();
        InitializeTexture();

        diffuseKernel = computeShader.FindKernel("Diffuse");
        linearSolveKernel = computeShader.FindKernel("LinearSolve");
        projectKernel1 = computeShader.FindKernel("Project1");
        projectKernel2 = computeShader.FindKernel("Project2");
        advectKernel = computeShader.FindKernel("Advect");
        setBoundaryKernel = computeShader.FindKernel("SetBoundary");
        addSourceKernel = computeShader.FindKernel("AddSource");
    }

    private void InitializeBuffers()
    {
        int size = resolution * resolution;
        densityArray = new float[size];

        density = new ComputeBuffer(size, sizeof(float));
        densityPrev = new ComputeBuffer(size, sizeof(float));
        velocityX = new ComputeBuffer(size, sizeof(float));
        velocityY = new ComputeBuffer(size, sizeof(float));
        velocityXPrev = new ComputeBuffer(size, sizeof(float));
        velocityYPrev = new ComputeBuffer(size, sizeof(float));
        vorticity = new ComputeBuffer(size, sizeof(float));
        curl = new ComputeBuffer(size, sizeof(float));

        float[] zeros = new float[size];
        density.SetData(zeros);
        densityPrev.SetData(zeros);
        velocityX.SetData(zeros);
        velocityY.SetData(zeros);
        velocityXPrev.SetData(zeros);
        velocityYPrev.SetData(zeros);
        vorticity.SetData(zeros);
        curl.SetData(zeros);
    }

    private void InitializeTexture()
    {
        texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        rend = GetComponent<Renderer>();
        rend.material.mainTexture = texture;
    }

    private void Update()
    {
        HandleInput();
        Simulate(Time.deltaTime);
        UpdateTexture();
    }

    private void OnDestroy()
    {
        density.Release();
        densityPrev.Release();
        velocityX.Release();
        velocityY.Release();
        velocityXPrev.Release();
        velocityYPrev.Release();
        vorticity.Release();
        curl.Release();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButton(0))
        {
            AddSourceFromMouse();
        }
    }

    private void AddSourceFromMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector2 uv = hit.textureCoord;
            int x = (int)(uv.x * resolution);
            int y = (int)(uv.y * resolution);
            Vector2 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

            computeShader.SetInt("sourceX", x);
            computeShader.SetInt("sourceY", y);
            computeShader.SetFloat("sourceRadius", sourceRadius);
            computeShader.SetFloat("sourceAmount", Random.Range(0.5f, 1f));
            computeShader.SetFloat("sourceAmountX", 0);
            computeShader.SetFloat("sourceAmountY", 0);
            computeShader.SetBuffer(addSourceKernel, "sourceField", density);

            int threadGroups = Mathf.CeilToInt(resolution / 8f);
            computeShader.Dispatch(addSourceKernel, threadGroups, threadGroups, 1);

            if (mouseDelta.magnitude > 0.01f)
            {
                computeShader.SetFloat("sourceAmount", 0);
                computeShader.SetFloat("sourceAmountX", mouseDelta.x * force);
                computeShader.SetFloat("sourceAmountY", mouseDelta.y * force);
                computeShader.SetBuffer(addSourceKernel, "sourceField", velocityX);
                computeShader.SetBuffer(addSourceKernel, "sourceField2", velocityY);

                computeShader.Dispatch(addSourceKernel, threadGroups, threadGroups, 1);
            }
        }
    }

    private void Simulate(float dt)
    {
        computeShader.SetInt("resolution", resolution);
        computeShader.SetFloat("dt", dt);
        computeShader.SetFloat("viscosity", viscosity);
        computeShader.SetFloat("diffusion", diffusion);

        Diffuse(1, velocityXPrev, velocityX, viscosity, dt);
        Diffuse(2, velocityYPrev, velocityY, viscosity, dt);

        Project(velocityXPrev, velocityYPrev, velocityX, velocityY);

        Advect(1, velocityX, velocityXPrev, velocityXPrev, velocityYPrev, dt);
        Advect(2, velocityY, velocityYPrev, velocityXPrev, velocityYPrev, dt);

        Project(velocityX, velocityY, velocityXPrev, velocityYPrev);

        Diffuse(0, densityPrev, density, diffusion, dt);
        Advect(0, density, densityPrev, velocityX, velocityY, dt);
    }

    private void Diffuse(int b, ComputeBuffer x, ComputeBuffer x0, float diff, float dt)
    {
        float a = dt * diff * (resolution - 2) * (resolution - 2);

        computeShader.SetInt("boundaryType", b);
        computeShader.SetFloat("a", a);
        computeShader.SetFloat("c", 1 + 6 * a);
        computeShader.SetBuffer(diffuseKernel, "x", x);
        computeShader.SetBuffer(diffuseKernel, "x0", x0);

        int threadGroups = Mathf.CeilToInt(resolution / 8f);

        for (int k = 0; k < 20; k++)
        {
            computeShader.Dispatch(diffuseKernel, threadGroups, threadGroups, 1);
            SetBoundary(b, x);
        }
    }

    private void LinearSolve(int b, ComputeBuffer x, ComputeBuffer x0, float a, float c)
    {
        computeShader.SetInt("boundaryType", b);
        computeShader.SetFloat("a", a);
        computeShader.SetFloat("c", c);
        computeShader.SetBuffer(linearSolveKernel, "x", x);
        computeShader.SetBuffer(linearSolveKernel, "x0", x0);

        int threadGroups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(linearSolveKernel, threadGroups, threadGroups, 1);
    }

    private void Project(ComputeBuffer velocX, ComputeBuffer velocY, ComputeBuffer p, ComputeBuffer div)
    {
        computeShader.SetBuffer(projectKernel1, "velocX", velocX);
        computeShader.SetBuffer(projectKernel1, "velocY", velocY);
        computeShader.SetBuffer(projectKernel1, "p", p);
        computeShader.SetBuffer(projectKernel1, "div", div);

        int threadGroups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(projectKernel1, threadGroups, threadGroups, 1);

        SetBoundary(0, div);
        SetBoundary(0, p);

        for (int k = 0; k < 20; k++)
        {
            LinearSolve(0, p, div, 1, 6);
        }

        computeShader.SetBuffer(projectKernel2, "velocX", velocX);
        computeShader.SetBuffer(projectKernel2, "velocY", velocY);
        computeShader.SetBuffer(projectKernel2, "p", p);
        computeShader.Dispatch(projectKernel2, threadGroups, threadGroups, 1);

        SetBoundary(1, velocX);
        SetBoundary(2, velocY);
    }

    private void Advect(int b, ComputeBuffer d, ComputeBuffer d0, ComputeBuffer velocX, ComputeBuffer velocY, float dt)
    {
        computeShader.SetInt("boundaryType", b);
        computeShader.SetBuffer(advectKernel, "d", d);
        computeShader.SetBuffer(advectKernel, "d0", d0);
        computeShader.SetBuffer(advectKernel, "velocX", velocX);
        computeShader.SetBuffer(advectKernel, "velocY", velocY);

        int threadGroups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(advectKernel, threadGroups, threadGroups, 1);

        SetBoundary(b, d);
    }

    private void SetBoundary(int b, ComputeBuffer x)
    {
        computeShader.SetInt("boundaryType", b);
        computeShader.SetBuffer(setBoundaryKernel, "x", x);

        int threadGroups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(setBoundaryKernel, threadGroups, threadGroups, 1);
    }

    private void UpdateTexture()
    {
        density.GetData(densityArray);

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int idx = i + j * resolution;
                float d = Mathf.Clamp01(densityArray[idx]);
                Color color = colorGradient.Evaluate(d);

                texture.SetPixel(i, j, color);
            }
        }

        texture.Apply();
    }
}