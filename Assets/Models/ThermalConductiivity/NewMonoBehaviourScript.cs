using System;
using System.Diagnostics;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;
using Debug = UnityEngine.Debug;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public ComputeShader ComputeShader;

    private ThermalConductivity thermalConductivity;

    void Start()
    {
        thermalConductivity = new ThermalConductivity();
        int gridSize = 8192;

        Stopwatch time = Stopwatch.StartNew();
        //thermalConductivity.ExecuteWithoutOptimization(gridSize, out double[] grid, out int iters);
        thermalConductivity.ExecuteGPUParallel(ComputeShader, gridSize, out double[] grid, out int iters);
        time.Stop();

        //DebugGrid(grid, gridSize);

        Debug.Log(time.ElapsedMilliseconds + " ms");
        Debug.Log("iters - " + iters);
    }

    private void DebugGrid(double[] grid, int gridSize)
    {
        string output = "";
        for (int i = 0; i < gridSize; i++)
        {
            for (int j = 0; j < gridSize; j++)
            {
                output += grid[i * gridSize + j].ToString() + "\t";
            }
            output += "\n";
        }
        Debug.Log(output);
    }
}
