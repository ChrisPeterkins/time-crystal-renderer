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
    private readonly int _delayMs;
    private readonly int _trailLength;

    /// <summary>
    /// Fired on the background thread each time a new mesh snapshot is ready.
    /// </summary>
    public event Action<TriangleMesh, int>? MeshUpdated;

    /// <summary>
    /// Fired when generation is complete.
    /// </summary>
    public event Action<TriangleMesh>? Completed;

    /// <param name="trailLength">How many generations a dead cell stays partially solid. 0 = no trails.</param>
    public CrystalGenerator(IAutomatonEngine engine, int totalGenerations,
                            int snapshotInterval = 50, bool smooth = false,
                            int delayMs = 0, int trailLength = 3)
    {
        _engine = engine;
        _totalGenerations = totalGenerations;
        _snapshotInterval = snapshotInterval;
        _smooth = smooth;
        _delayMs = delayMs;
        _trailLength = trailLength;
    }

    /// <summary>
    /// Runs the full generation pipeline. Call this from a background thread.
    /// </summary>
    public void Generate(CancellationToken cancellationToken = default)
    {
        int width = _engine.Width;
        int height = _engine.Height;
        var field = new float[width * height * _totalGenerations];
        var lastAliveAt = new int[width * height];
        Array.Fill(lastAliveAt, -_trailLength - 1);

        var extractor = new MarchingCubesExtractor();

        for (int z = 0; z < _totalGenerations; z++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Capture current generation with trails
            CaptureWithTrails(field, lastAliveAt, z, width, height);
            _engine.Step();

            // Emit a snapshot at each interval and at the final generation
            bool isSnapshot = (z + 1) % _snapshotInterval == 0;
            bool isFinal = z == _totalGenerations - 1;

            if (isSnapshot || isFinal)
            {
                int completedGenerations = z + 1;
                var mesh = ExtractFromField(field, width, height, completedGenerations, extractor);
                MeshUpdated?.Invoke(mesh, completedGenerations);

                if (_delayMs > 0 && !isFinal)
                    Thread.Sleep(_delayMs);
            }
        }

        // Final mesh with full post-processing
        var finalMesh = ExtractFromField(field, width, height, _totalGenerations, extractor);
        finalMesh.DeduplicateAndSmoothNormals();
        Completed?.Invoke(finalMesh);
    }

    private void CaptureWithTrails(float[] field, int[] lastAliveAt,
                                   int z, int width, int height)
    {
        var state = _engine.CurrentState;

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
                else if (_trailLength > 0)
                {
                    int generationsSinceDeath = z - lastAliveAt[cellIndex];
                    if (generationsSinceDeath <= _trailLength)
                    {
                        float fade = 1f - (float)generationsSinceDeath / (_trailLength + 1);
                        field[x + y * width + z * width * height] = fade;
                    }
                }
            }
        }
    }

    private TriangleMesh ExtractFromField(float[] field, int width, int height,
                                          int completedGenerations, MarchingCubesExtractor extractor)
    {
        int sliceSize = width * height;

        float Sampler(int x, int y, int z)
        {
            if (x < 0 || x >= width || y < 0 || y >= height ||
                z < 0 || z >= completedGenerations)
                return 0f;
            return field[x + y * width + z * sliceSize];
        }

        int sizeZ = Math.Min(completedGenerations + 1, _totalGenerations);
        var mesh = extractor.Extract(width, height, sizeZ, Sampler, ColorMapper.GenerationToColor);

        return mesh;
    }
}
