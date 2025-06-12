using System;
using System.Threading;
using UnityEngine;

public class ThermalConductivity
{
    private const float a = 1.9f * 1e-5f; // температуропроводность воздуха
    private const double eps = 1e-5f;
    private const float dt = 0.01f; // шаг по времени

    public void ExecuteWithoutOptimization(int gridSize, out double[] grid, out int iters)
    {
        grid = new double[gridSize * gridSize];
        double[] newGrid = new double[gridSize * gridSize];
        nx = gridSize;
        ny = gridSize;

        double dx = 1.0 / (nx - 1);
        double r = a * dt / (dx * dx);

        newGrid[gridSize + 1] = grid[gridSize + 1] = 1;

        iters = 0;
        while (true)
        {
            iters++;
            for (int i = 1; i < ny - 1; i++)
            {
                for (int j = 1; j < nx - 1; j++)
                {
                    newGrid[IND(i, j)] = grid[IND(i, j)] + r * (
                        grid[IND(i - 1, j)] + grid[IND(i + 1, j)] +
                        grid[IND(i, j - 1)] + grid[IND(i, j + 1)] -
                        4.0 * grid[IND(i, j)]);
                }
            }

            double maxdiff = 0;
            for (int i = 1; i < ny - 1; i++)
            {
                for (int j = 1; j < nx - 1; j++)
                {
                    int ind = IND(i, j);
                    maxdiff = Math.Max(maxdiff, Math.Abs(grid[ind] - newGrid[ind]));
                }
            }

            (grid, newGrid) = (newGrid, grid);

            if (maxdiff < eps) { break; }
        }
    }

    ComputeBuffer gridBuffer;
    ComputeBuffer newGridBuffer;
    ComputeBuffer maxDiffBuffer;
    public void ExecuteGPUParallel(ComputeShader computeShader, int gridSize, out double[] grid, out int iters)
    {
        int totalCells = gridSize * gridSize;
        gridBuffer = new ComputeBuffer(totalCells, sizeof(double));
        newGridBuffer = new ComputeBuffer(totalCells, sizeof(double));
        ComputeBuffer maxDiffBuffer = new ComputeBuffer(1, sizeof(double));

        //init
        int kernelInit = computeShader.FindKernel("ThermalConductivityInit");
        computeShader.SetInt("GridSize", gridSize);
        computeShader.SetFloat("A", a);
        computeShader.SetFloat("Dt", dt);
        computeShader.SetBuffer(kernelInit, "Grid", gridBuffer);
        computeShader.SetBuffer(kernelInit, "NewGrid", newGridBuffer);

        int threadGroups = Mathf.CeilToInt(gridSize / 16f);
        computeShader.Dispatch(kernelInit, threadGroups, threadGroups, 1);

        //step
        int kernelStep = computeShader.FindKernel("ThermalConductivityStep");
        computeShader.SetBuffer(kernelStep, "Grid", gridBuffer);
        computeShader.SetBuffer(kernelStep, "NewGrid", newGridBuffer);

        int kernelSwap = computeShader.FindKernel("ThermalConductivitySwap");
        computeShader.SetBuffer(kernelSwap, "Grid", gridBuffer);
        computeShader.SetBuffer(kernelSwap, "NewGrid", newGridBuffer);
        computeShader.SetBuffer(kernelSwap, "MaxDiff", maxDiffBuffer);

        double[] maxDiff = new double[1];
        iters = 0;
        do
        {
            computeShader.Dispatch(kernelStep, threadGroups, threadGroups, 1);
            computeShader.Dispatch(kernelSwap, threadGroups, threadGroups, 1);

            maxDiffBuffer.GetData(maxDiff);
            iters++;
        } while (maxDiff[0] > eps);
        
        grid = new double[gridSize *  gridSize];
        gridBuffer.GetData(grid);

        gridBuffer?.Release();
        newGridBuffer?.Release();
        maxDiffBuffer?.Release();
    }

    int nx;
    int ny;
    private int IND(int i, int j) { return (i) * nx + j; }
}
