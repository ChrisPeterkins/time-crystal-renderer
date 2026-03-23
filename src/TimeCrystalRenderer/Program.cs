using System.Diagnostics;
using Silk.NET.Input;
using TimeCrystalRenderer.Core;
using TimeCrystalRenderer.Core.Automata;
using TimeCrystalRenderer.Core.MarchingCubes;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Core.Voxel;
using TimeCrystalRenderer.Renderer;

const int GridSize = 128;
const int Generations = 300;
const int SnapshotInterval = 10;
const int GrowthDelayMs = 500;
const string StlPath = "time_crystal.stl";
const string ObjPath = "time_crystal.obj";

bool isSmooth = true;
string currentPattern = "Soup";
bool isLiveMode = true;

Console.WriteLine("=== Time Crystal Renderer ===\n");

// Initial launch goes straight to live growth mode
LaunchViewer();

void LaunchViewer()
{
    var emptyMesh = new TriangleMesh(0);
    using var window = new RenderWindow(emptyMesh, StlPath, ObjPath);

    CancellationTokenSource? activeCancellation = null;

    window.RegenerationRequested += key =>
    {
        // Cancel any in-progress generation
        activeCancellation?.Cancel();

        switch (key)
        {
            case Key.Number1: currentPattern = "R-pentomino"; break;
            case Key.Number2: currentPattern = "Glider Gun"; break;
            case Key.Number3: currentPattern = "Acorn"; break;
            case Key.Number4: currentPattern = "Random"; break;
            case Key.Number5: currentPattern = "Glider"; break;
            case Key.Number6: currentPattern = "Soup"; break;
            case Key.T:
                isSmooth = !isSmooth;
                Console.WriteLine($"Smoothing: {(isSmooth ? "ON" : "OFF")}");
                break;
            case Key.L:
                isLiveMode = !isLiveMode;
                Console.WriteLine($"Live growth: {(isLiveMode ? "ON" : "OFF")}");
                break;
            case Key.R:
                break; // Reload same pattern
        }

        activeCancellation = new CancellationTokenSource();
        var token = activeCancellation.Token;

        if (isLiveMode)
        {
            StartLiveGrowth(window, token);
        }
        else
        {
            Task.Run(() =>
            {
                var mesh = GenerateBatch(currentPattern, isSmooth);
                if (!token.IsCancellationRequested)
                    window.QueueMeshUpdate(mesh, Generations);
            }, token);
        }
    };

    // Start initial live growth
    activeCancellation = new CancellationTokenSource();
    StartLiveGrowth(window, activeCancellation.Token);

    PrintControls();
    window.Run();

    activeCancellation.Cancel();
}

void StartLiveGrowth(RenderWindow window, CancellationToken token)
{
    Task.Run(() =>
    {
        Console.WriteLine($"\nGrowing: {currentPattern} (smooth: {isSmooth}, live mode)");

        var engine = new GameOfLifeEngine(GridSize, GridSize);
        ApplyPattern(engine, currentPattern);

        var generator = new CrystalGenerator(engine, Generations, SnapshotInterval, isSmooth, GrowthDelayMs);

        generator.MeshUpdated += (mesh, gen) =>
        {
            if (!token.IsCancellationRequested)
            {
                Console.WriteLine($"  Gen {gen}/{Generations}: {mesh.TriangleCount:N0} tris");
                window.QueueMeshUpdate(mesh, gen);
            }
        };

        generator.Completed += finalMesh =>
        {
            if (!token.IsCancellationRequested)
            {
                Console.WriteLine($"  Complete: {finalMesh.TriangleCount:N0} tris, {finalMesh.VertexCount:N0} verts");
                window.QueueMeshUpdate(finalMesh, Generations);
            }
        };

        generator.Generate(token);
    }, token);
}

TriangleMesh GenerateBatch(string pattern, bool useSmoothing)
{
    const int TrailLength = 3;
    var stopwatch = Stopwatch.StartNew();
    Console.WriteLine($"\nGenerating: {pattern} (smooth: {useSmoothing}, trails: {TrailLength})...");

    var engine = new GameOfLifeEngine(GridSize, GridSize);
    ApplyPattern(engine, pattern);

    // Build with trails so dead cells fade instead of vanishing,
    // producing thicker, more connected structures
    var trailField = VoxelVolumeBuilder.BuildWithTrails(engine, Generations, TrailLength);
    int width = GridSize;
    int height = GridSize;
    int sliceSize = width * height;

    float Sampler(int x, int y, int z)
    {
        if (x < 0 || x >= width || y < 0 || y >= height ||
            z < 0 || z >= Generations)
            return 0f;
        return trailField[x + y * width + z * sliceSize];
    }

    var extractor = new MarchingCubesExtractor();
    var result = extractor.Extract(width, height, Generations, Sampler, ColorMapper.GenerationToColor);

    int beforeVertices = result.VertexCount;
    result.DeduplicateAndSmoothNormals();
    Console.WriteLine($"  Done in {stopwatch.ElapsedMilliseconds}ms: {result.TriangleCount:N0} tris, " +
                      $"{beforeVertices:N0} -> {result.VertexCount:N0} verts");
    return result;
}

void ApplyPattern(IAutomatonEngine engine, string pattern)
{
    // Random offset so the pattern lands in a different spot each run
    var (offsetX, offsetY) = PatternLibrary.RandomOffset(engine, margin: 20);

    switch (pattern)
    {
        case "R-pentomino":
            PatternLibrary.ApplyRPentomino(engine, offsetX, offsetY);
            break;
        case "Glider Gun":
            PatternLibrary.ApplyGliderGun(engine, offsetX, offsetY);
            break;
        case "Acorn":
            PatternLibrary.ApplyAcorn(engine, offsetX, offsetY);
            break;
        case "Random":
            PatternLibrary.ApplyRandom(engine, density: 0.3);
            return; // Already fully random, no perturbation needed
        case "Glider":
            PatternLibrary.ApplyGlider(engine, offsetX, offsetY);
            break;
        case "Soup":
            PatternLibrary.ApplySoup(engine, density: 0.5, regionFraction: 0.4);
            return; // Already random
    }

    // Scatter some random cells near the pattern — makes every run unique
    PatternLibrary.Perturb(engine, cellCount: 12, radius: 20);
}

void PrintControls()
{
    Console.WriteLine("Viewer controls:");
    Console.WriteLine("  Left-drag:   Rotate");
    Console.WriteLine("  Scroll:      Zoom");
    Console.WriteLine("  Middle-drag: Pan");
    Console.WriteLine();
    Console.WriteLine("  1: R-pentomino    2: Glider Gun    3: Acorn");
    Console.WriteLine("  4: Random (full)  5: Glider        6: Soup (central blob)");
    Console.WriteLine("  T: Toggle smooth  L: Toggle live growth");
    Console.WriteLine("  R: Reload (same pattern)");
    Console.WriteLine();
    Console.WriteLine("  S: Save STL       O: Save OBJ");
    Console.WriteLine("  Escape: Quit");
}
