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
