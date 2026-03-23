using TimeCrystalRenderer.Core.Automata;

namespace TimeCrystalRenderer.Core.Voxel;

/// <summary>
/// Runs an automaton for a number of generations and captures each frame into a voxel volume.
/// </summary>
public static class VoxelVolumeBuilder
{
    public static VoxelVolume Build(IAutomatonEngine engine, int generations, IProgress<int>? progress = null)
    {
        if (generations <= 0)
            throw new ArgumentOutOfRangeException(nameof(generations));

        var volume = new VoxelVolume(engine.Width, engine.Height, generations);

        for (int z = 0; z < generations; z++)
        {
            CaptureGeneration(engine, volume, z);
            engine.Step();
            progress?.Report(z + 1);
        }

        return volume;
    }

    /// <summary>
    /// Builds a volume with cell trails — when a cell dies, it fades over several generations
    /// instead of vanishing instantly. This produces thicker, more connected structures.
    /// The trail creates a smooth float field directly (no separate blur needed, though
    /// blur can still be applied on top for even smoother results).
    /// </summary>
    /// <param name="trailLength">How many generations a dead cell remains partially solid.</param>
    public static float[] BuildWithTrails(IAutomatonEngine engine, int generations,
                                          int trailLength = 3, IProgress<int>? progress = null)
    {
        if (generations <= 0)
            throw new ArgumentOutOfRangeException(nameof(generations));

        int width = engine.Width;
        int height = engine.Height;
        var field = new float[width * height * generations];

        // Track how many generations ago each cell was last alive
        var lastAliveAt = new int[width * height];
        Array.Fill(lastAliveAt, -trailLength - 1);

        for (int z = 0; z < generations; z++)
        {
            var state = engine.CurrentState;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cellIndex = y * width + x;
                    bool isAlive = state[cellIndex] != 0;

                    if (isAlive)
                    {
                        lastAliveAt[cellIndex] = z;
                        field[x + y * width + z * width * height] = 1f;
                    }
                    else
                    {
                        // Fade based on how recently the cell was alive
                        int generationsSinceDeath = z - lastAliveAt[cellIndex];

                        if (generationsSinceDeath <= trailLength)
                        {
                            float fade = 1f - (float)generationsSinceDeath / (trailLength + 1);
                            field[x + y * width + z * width * height] = fade;
                        }
                        // else: stays 0 (default)
                    }
                }
            }

            engine.Step();
            progress?.Report(z + 1);
        }

        return field;
    }

    private static void CaptureGeneration(IAutomatonEngine engine, VoxelVolume volume, int z)
    {
        var state = engine.CurrentState;

        for (int y = 0; y < engine.Height; y++)
        {
            for (int x = 0; x < engine.Width; x++)
            {
                if (state[y * engine.Width + x] != 0)
                    volume[x, y, z] = true;
            }
        }
    }
}
