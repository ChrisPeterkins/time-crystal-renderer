using System.Diagnostics;
using TimeCrystalRenderer.Core;
using TimeCrystalRenderer.Core.Automata;
using TimeCrystalRenderer.Core.MarchingCubes;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Core.Voxel;

const int GridSize = 64;
const int Generations = 200;
const string OutputPath = "time_crystal.stl";

Console.WriteLine("=== Time Crystal Renderer ===\n");

// Step 1: Set up the simulation
var engine = new GameOfLifeEngine(GridSize, GridSize);
PatternLibrary.ApplyRPentomino(engine);
Console.WriteLine($"Pattern: R-pentomino on {GridSize}x{GridSize} grid");
Console.WriteLine($"Generations: {Generations}\n");

// Step 2: Build the voxel volume
Console.Write("Building voxel volume... ");
var stopwatch = Stopwatch.StartNew();

var volume = VoxelVolumeBuilder.Build(engine, Generations);

stopwatch.Stop();
long aliveCount = CountAliveVoxels(volume);
Console.WriteLine($"done in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"  Volume: {volume.SizeX}x{volume.SizeY}x{volume.SizeZ} ({volume.TotalVoxels:N0} voxels, {aliveCount:N0} alive)\n");

// Step 3: Extract mesh with marching cubes
Console.Write("Extracting mesh... ");
stopwatch.Restart();

var extractor = new MarchingCubesExtractor();
var mesh = extractor.Extract(volume, ColorMapper.GenerationToColor);

stopwatch.Stop();
Console.WriteLine($"done in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"  Triangles: {mesh.TriangleCount:N0}  Vertices: {mesh.VertexCount:N0}\n");

// Step 4: Export to STL
Console.Write($"Exporting to {OutputPath}... ");
StlExporter.Export(mesh, OutputPath);

var fileInfo = new FileInfo(OutputPath);
Console.WriteLine($"done ({fileInfo.Length / 1024.0 / 1024.0:F1} MB)");
Console.WriteLine($"\nOpen {OutputPath} in macOS Preview or MeshLab to see your time crystal.");

static long CountAliveVoxels(VoxelVolume volume)
{
    long count = 0;
    for (int z = 0; z < volume.SizeZ; z++)
        for (int y = 0; y < volume.SizeY; y++)
            for (int x = 0; x < volume.SizeX; x++)
                if (volume[x, y, z])
                    count++;
    return count;
}
