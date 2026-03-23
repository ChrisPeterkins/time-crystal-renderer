using System.Numerics;
using TimeCrystalRenderer.Core.Automata;
using TimeCrystalRenderer.Core.MarchingCubes;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Core.Voxel;

namespace TimeCrystalRenderer.Core;

/// <summary>
/// Generates time crystals progressively, emitting mesh snapshots at regular intervals.
/// Designed to run on a background thread so the viewer can display the crystal as it grows.
/// </summary>
public sealed class CrystalGenerator
{
    private readonly IAutomatonEngine _engine;
    private readonly int _totalGenerations;
    private readonly int _snapshotInterval;
    private readonly bool _smooth;

    /// <summary>
    /// Fired on the background thread each time a new mesh snapshot is ready.
    /// The viewer should queue this for GPU upload on the render thread.
    /// </summary>
    public event Action<TriangleMesh, int>? MeshUpdated;

    /// <summary>
    /// Fired when generation is complete.
    /// </summary>
    public event Action<TriangleMesh>? Completed;

    public CrystalGenerator(IAutomatonEngine engine, int totalGenerations,
                            int snapshotInterval = 50, bool smooth = false)
    {
        _engine = engine;
        _totalGenerations = totalGenerations;
        _snapshotInterval = snapshotInterval;
        _smooth = smooth;
    }

    /// <summary>
    /// Runs the full generation pipeline. Call this from a background thread.
    /// </summary>
    public void Generate(CancellationToken cancellationToken = default)
    {
        var volume = new VoxelVolume(_engine.Width, _engine.Height, _totalGenerations);
        var extractor = new MarchingCubesExtractor();

        for (int z = 0; z < _totalGenerations; z++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Capture current generation into the volume
            var state = _engine.CurrentState;
            for (int y = 0; y < _engine.Height; y++)
                for (int x = 0; x < _engine.Width; x++)
                    if (state[y * _engine.Width + x] != 0)
                        volume[x, y, z] = true;

            _engine.Step();

            // Emit a snapshot at each interval and at the final generation
            bool isSnapshot = (z + 1) % _snapshotInterval == 0;
            bool isFinal = z == _totalGenerations - 1;

            if (isSnapshot || isFinal)
            {
                var mesh = ExtractMesh(volume, z + 1, extractor);
                MeshUpdated?.Invoke(mesh, z + 1);
            }
        }

        // Final mesh with full post-processing
        var finalMesh = ExtractMesh(volume, _totalGenerations, extractor);
        finalMesh.DeduplicateAndSmoothNormals();
        Completed?.Invoke(finalMesh);
    }

    private TriangleMesh ExtractMesh(VoxelVolume volume, int generationsSoFar,
                                     MarchingCubesExtractor extractor)
    {
        if (_smooth)
        {
            var smoothedField = VolumeSmoothing.BoxBlur3D(volume);
            int sizeX = volume.SizeX;
            int sizeY = volume.SizeY;

            // Create a sampler that reads from the smoothed field, clamped to the
            // generations computed so far (avoids meshing empty future layers)
            float Sampler(int x, int y, int z)
            {
                if (z >= generationsSoFar || x < 0 || x >= volume.SizeX ||
                    y < 0 || y >= volume.SizeY || z < 0 || z >= volume.SizeZ)
                    return 0f;
                return smoothedField[x + y * sizeX + z * sizeX * sizeY];
            }

            return extractor.Extract(volume.SizeX, volume.SizeY,
                                     Math.Min(generationsSoFar + 1, volume.SizeZ),
                                     Sampler, ColorMapper.GenerationToColor);
        }

        // Binary extraction limited to generations computed so far
        float BinarySampler(int x, int y, int z)
        {
            if (z >= generationsSoFar)
                return 0f;
            return volume.Sample(x, y, z);
        }

        return extractor.Extract(volume.SizeX, volume.SizeY,
                                 Math.Min(generationsSoFar + 1, volume.SizeZ),
                                 BinarySampler, ColorMapper.GenerationToColor);
    }
}
