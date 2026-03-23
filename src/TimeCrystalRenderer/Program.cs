using TimeCrystalRenderer.Core.Automata;

const int GridSize = 20;
const int Generations = 20;

var engine = new GameOfLifeEngine(GridSize, GridSize);
PatternLibrary.ApplyGlider(engine, offsetX: 1, offsetY: 1);

Console.WriteLine("=== Time Crystal Renderer — Phase 1: Game of Life ===\n");
Console.WriteLine("Pattern: Glider on a {0}x{0} grid", GridSize);
Console.WriteLine("Watch it move diagonally one cell every 4 generations.\n");

for (int generation = 0; generation <= Generations; generation++)
{
    Console.WriteLine($"Generation {generation}:");
    PrintGrid(engine);
    Console.WriteLine();

    if (generation < Generations)
        engine.Step();
}

static void PrintGrid(IAutomatonEngine engine)
{
    for (int y = 0; y < engine.Height; y++)
    {
        for (int x = 0; x < engine.Width; x++)
            Console.Write(engine.GetCell(x, y) ? "██" : "··");

        Console.WriteLine();
    }
}
