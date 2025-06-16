using UnityEngine;

public class NavierStokes : MonoBehaviour
{
    [Header("Simulation Settings")]
    [Range(16, 256)] public int resolution = 128;
    [Range(0, 0.01f)] public float viscosity = 0.0001f;
    [Range(0, 0.01f)] public float diffusion = 0.0001f;
    [Range(0, 50f)] public float force = 10f;
    [Range(1, 20f)] public float sourceRadius = 10f;

    [Header("Visualization")]
    public Gradient colorGradient;

    private float[] density;
    private float[] densityPrev;
    private float[] velocityX;
    private float[] velocityY;
    private float[] velocityXPrev;
    private float[] velocityYPrev;

    private Texture2D texture;
    private Renderer rend;

    private void Start()
    {
        InitializeArrays();
        InitializeTexture();
    }

    private void InitializeArrays()
    {
        int size = resolution * resolution;
        density = new float[size];
        densityPrev = new float[size];
        velocityX = new float[size];
        velocityY = new float[size];
        velocityXPrev = new float[size];
        velocityYPrev = new float[size];
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
            AddDensity(x, y, Random.Range(0.5f, 1f));
            AddVelocity(x, y, mouseDelta.x * force, mouseDelta.y * force);
        }
    }

    private void Simulate(float dt)
    {
        Diffuse(1, velocityXPrev, velocityX, viscosity, dt);
        Diffuse(2, velocityYPrev, velocityY, viscosity, dt);

        Project(velocityXPrev, velocityYPrev, velocityX, velocityY);

        Advect(1, velocityX, velocityXPrev, velocityXPrev, velocityYPrev, dt);
        Advect(2, velocityY, velocityYPrev, velocityXPrev, velocityYPrev, dt);

        Project(velocityX, velocityY, velocityXPrev, velocityYPrev);

        Diffuse(0, densityPrev, density, diffusion, dt);
        Advect(0, density, densityPrev, velocityX, velocityY, dt);
    }

    private void Diffuse(int b, float[] x, float[] x0, float diff, float dt)
    {
        float a = dt * diff * (resolution - 2) * (resolution - 2);
        LinearSolve(b, x, x0, a, 1 + 6 * a);
    }

    private void LinearSolve(int b, float[] x, float[] x0, float a, float c)
    {
        for (int k = 0; k < 20; k++)
        {
            for (int i = 1; i < resolution - 1; i++)
            {
                for (int j = 1; j < resolution - 1; j++)
                {
                    int idx = i + j * resolution;
                    x[idx] = (x0[idx] + a * (x[idx - 1] + x[idx + 1] +
                             x[idx - resolution] + x[idx + resolution])) / c;
                }
            }
            SetBoundary(b, x);
        }
    }

    private void Project(float[] velocX, float[] velocY, float[] p, float[] div)
    {
        for (int i = 1; i < resolution - 1; i++)
        {
            for (int j = 1; j < resolution - 1; j++)
            {
                int idx = i + j * resolution;
                div[idx] = -0.5f * (velocX[idx + 1] - velocX[idx - 1] +
                          velocY[idx + resolution] - velocY[idx - resolution]) / resolution;
                p[idx] = 0;
            }
        }

        SetBoundary(0, div);
        SetBoundary(0, p);
        LinearSolve(0, p, div, 1, 6);

        for (int i = 1; i < resolution - 1; i++)
        {
            for (int j = 1; j < resolution - 1; j++)
            {
                int idx = i + j * resolution;
                velocX[idx] -= 0.5f * (p[idx + 1] - p[idx - 1]) * resolution;
                velocY[idx] -= 0.5f * (p[idx + resolution] - p[idx - resolution]) * resolution;
            }
        }

        SetBoundary(1, velocX);
        SetBoundary(2, velocY);
    }

    private void Advect(int b, float[] d, float[] d0, float[] velocX, float[] velocY, float dt)
    {
        for (int i = 1; i < resolution - 1; i++)
        {
            for (int j = 1; j < resolution - 1; j++)
            {
                int idx = i + j * resolution;

                float x = i - dt * velocX[idx] * resolution;
                float y = j - dt * velocY[idx] * resolution;

                x = Mathf.Clamp(x, 0.5f, resolution - 1.5f);
                y = Mathf.Clamp(y, 0.5f, resolution - 1.5f);

                int i0 = (int)x;
                int j0 = (int)y;
                int i1 = i0 + 1;
                int j1 = j0 + 1;

                float s1 = x - i0;
                float s0 = 1 - s1;
                float t1 = y - j0;
                float t0 = 1 - t1;

                d[idx] = s0 * (t0 * d0[i0 + j0 * resolution] + t1 * d0[i0 + j1 * resolution]) +
                         s1 * (t0 * d0[i1 + j0 * resolution] + t1 * d0[i1 + j1 * resolution]);
            }
        }

        SetBoundary(b, d);
    }

    private void SetBoundary(int b, float[] x)
    {
        for (int i = 1; i < resolution - 1; i++)
        {
            x[i] = b == 1 ? -x[i + resolution] : x[i + resolution];
            x[i + (resolution - 1) * resolution] = b == 1 ? -x[i + (resolution - 2) * resolution] : x[i + (resolution - 2) * resolution];
        }

        for (int j = 1; j < resolution - 1; j++)
        {
            x[j * resolution] = b == 2 ? -x[1 + j * resolution] : x[1 + j * resolution];
            x[(resolution - 1) + j * resolution] = b == 2 ? -x[(resolution - 2) + j * resolution] : x[(resolution - 2) + j * resolution];
        }

        x[0] = 0.5f * (x[1] + x[resolution]);
        x[(resolution - 1)] = 0.5f * (x[resolution - 2] + x[resolution * 2 - 1]);
        x[(resolution - 1) * resolution] = 0.5f * (x[(resolution - 2) * resolution] + x[(resolution - 1) * resolution + 1]);
        x[resolution * resolution - 1] = 0.5f * (x[resolution * resolution - 2] + x[(resolution - 1) * resolution - 1]);
    }

    private void AddDensity(int x, int y, float amount)
    {
        for (int i = -Mathf.FloorToInt(sourceRadius); i <= sourceRadius; i++)
        {
            for (int j = -Mathf.FloorToInt(sourceRadius); j <= sourceRadius; j++)
            {
                int xi = x + i;
                int yj = y + j;

                if (xi >= 0 && xi < resolution && yj >= 0 && yj < resolution)
                {
                    float dist = Mathf.Sqrt(i * i + j * j);
                    if (dist <= sourceRadius)
                    {
                        int idx = xi + yj * resolution;
                        density[idx] += amount * (1 - dist / sourceRadius);
                    }
                }
            }
        }
    }

    private void AddVelocity(int x, int y, float amountX, float amountY)
    {
        for (int i = -Mathf.FloorToInt(sourceRadius); i <= sourceRadius; i++)
        {
            for (int j = -Mathf.FloorToInt(sourceRadius); j <= sourceRadius; j++)
            {
                int xi = x + i;
                int yj = y + j;

                if (xi >= 0 && xi < resolution && yj >= 0 && yj < resolution)
                {
                    float dist = Mathf.Sqrt(i * i + j * j);
                    if (dist <= sourceRadius)
                    {
                        int idx = xi + yj * resolution;
                        velocityX[idx] += amountX * (1 - dist / sourceRadius);
                        velocityY[idx] += amountY * (1 - dist / sourceRadius);
                    }
                }
            }
        }
    }

    private void UpdateTexture()
    {
        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                int idx = i + j * resolution;
                float d = Mathf.Clamp01(density[idx]);
                Color color = colorGradient.Evaluate(d);

                texture.SetPixel(i, j, color);
            }
        }

        texture.Apply();
    }
}