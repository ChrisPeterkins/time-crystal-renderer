using System.Diagnostics;
using Silk.NET.Input;
using TimeCrystalRenderer.Core;
using TimeCrystalRenderer.Core.Automata;
using TimeCrystalRenderer.Core.MarchingCubes;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Core.Voxel;
using TimeCrystalRenderer.Renderer;

const int GridSize = 64;
const int Generations = 200;
const string StlPath = "time_crystal.stl";
const string ObjPath = "time_crystal.obj";

bool smooth = true;
string currentPattern = "R-pentomino";

Console.WriteLine("=== Time Crystal Renderer ===\n");

// Generate the initial crystal
var mesh = GenerateCrystal(currentPattern, smooth);

// Launch viewer with regeneration support
using var window = new RenderWindow(mesh, StlPath, ObjPath);
window.RegenerationRequested += key => OnRegenerate(key, window);
window.Run();

void OnRegenerate(Key key, RenderWindow viewer)
{
    switch (key)
    {
        case Key.Number1: currentPattern = "R-pentomino"; break;
        case Key.Number2: currentPattern = "Glider Gun"; break;
        case Key.Number3: currentPattern = "Acorn"; break;
        case Key.Number4: currentPattern = "Random"; break;
        case Key.Number5: currentPattern = "Glider"; break;
        case Key.T:
            smooth = !smooth;
            Console.WriteLine($"Smoothing: {(smooth ? "ON" : "OFF")}");
            break;
        case Key.R:
            break; // Reload same pattern
    }

    // Run regeneration on a background thread to avoid blocking the render loop
    Task.Run(() =>
    {
        var newMesh = GenerateCrystal(currentPattern, smooth);
        viewer.QueueMeshUpdate(newMesh, Generations);
    });
}

TriangleMesh GenerateCrystal(string pattern, bool useSmoothing)
{
    var stopwatch = Stopwatch.StartNew();
    Console.WriteLine($"\nGenerating: {pattern} (smooth: {useSmoothing})...");

    var engine = new GameOfLifeEngine(GridSize, GridSize);
    ApplyPattern(engine, pattern);

    var volume = VoxelVolumeBuilder.Build(engine, Generations);

    var extractor = new MarchingCubesExtractor();
    TriangleMesh result;

    if (useSmoothing)
    {
        var smoothedField = VolumeSmoothing.BoxBlur3D(volume);
        int sizeX = volume.SizeX;
        int sizeY = volume.SizeY;

        float Sampler(int x, int y, int z)
        {
            if (x < 0 || x >= volume.SizeX || y < 0 || y >= volume.SizeY ||
                z < 0 || z >= volume.SizeZ)
                return 0f;
            return smoothedField[x + y * sizeX + z * sizeX * sizeY];
        }

        result = extractor.Extract(volume.SizeX, volume.SizeY, volume.SizeZ,
                                   Sampler, ColorMapper.GenerationToColor);
    }
    else
    {
        result = extractor.Extract(volume, ColorMapper.GenerationToColor);
    }

    int beforeVertices = result.VertexCount;
    result.DeduplicateAndSmoothNormals();

    Console.WriteLine($"  Done in {stopwatch.ElapsedMilliseconds}ms: {result.TriangleCount:N0} tris, " +
                      $"{beforeVertices:N0} -> {result.VertexCount:N0} verts");

    return result;
}

void ApplyPattern(IAutomatonEngine engine, string pattern)
{
    switch (pattern)
    {
        case "R-pentomino":
            PatternLibrary.ApplyRPentomino(engine);
            break;
        case "Glider Gun":
            PatternLibrary.ApplyGliderGun(engine);
            break;
        case "Acorn":
            PatternLibrary.ApplyAcorn(engine);
            break;
        case "Random":
            PatternLibrary.ApplyRandom(engine, density: 0.3);
            break;
        case "Glider":
            PatternLibrary.ApplyGlider(engine);
            break;
    }
}
