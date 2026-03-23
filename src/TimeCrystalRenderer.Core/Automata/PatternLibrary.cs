namespace TimeCrystalRenderer.Core.Automata;

/// <summary>
/// Predefined Game of Life patterns that can be stamped onto an automaton grid.
/// </summary>
public static class PatternLibrary
{
    /// <summary>
    /// Fills the grid randomly. Density 0.0 = empty, 1.0 = full.
    /// </summary>
    public static void ApplyRandom(IAutomatonEngine engine, double density = 0.3, int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        for (int y = 0; y < engine.Height; y++)
            for (int x = 0; x < engine.Width; x++)
                engine.SetCell(x, y, random.NextDouble() < density);
    }

    /// <summary>
    /// The smallest spaceship — moves diagonally one cell every 4 generations.
    /// </summary>
    public static void ApplyGlider(IAutomatonEngine engine, int offsetX = 1, int offsetY = 1)
    {
        // .#.
        // ..#
        // ###
        int[,] cells = { { 1, 0 }, { 2, 1 }, { 0, 2 }, { 1, 2 }, { 2, 2 } };
        StampPattern(engine, cells, offsetX, offsetY);
    }

    /// <summary>
    /// Gosper glider gun — emits a new glider every 30 generations.
    /// </summary>
    public static void ApplyGliderGun(IAutomatonEngine engine, int offsetX = 1, int offsetY = 1)
    {
        int[,] cells =
        {
            // Left block
            { 0, 4 }, { 0, 5 }, { 1, 4 }, { 1, 5 },
            // Left structure
            { 10, 4 }, { 10, 5 }, { 10, 6 },
            { 11, 3 }, { 11, 7 },
            { 12, 2 }, { 12, 8 },
            { 13, 2 }, { 13, 8 },
            { 14, 5 },
            { 15, 3 }, { 15, 7 },
            { 16, 4 }, { 16, 5 }, { 16, 6 },
            { 17, 5 },
            // Right structure
            { 20, 2 }, { 20, 3 }, { 20, 4 },
            { 21, 2 }, { 21, 3 }, { 21, 4 },
            { 22, 1 }, { 22, 5 },
            { 24, 0 }, { 24, 1 }, { 24, 5 }, { 24, 6 },
            // Right block
            { 34, 2 }, { 34, 3 }, { 35, 2 }, { 35, 3 },
        };
        StampPattern(engine, cells, offsetX, offsetY);
    }

    /// <summary>
    /// R-pentomino — a tiny pattern that evolves chaotically for 1103 generations.
    /// </summary>
    public static void ApplyRPentomino(IAutomatonEngine engine, int? offsetX = null, int? offsetY = null)
    {
        int x = offsetX ?? engine.Width / 2 - 1;
        int y = offsetY ?? engine.Height / 2 - 1;

        // .##
        // ##.
        // .#.
        int[,] cells = { { 1, 0 }, { 2, 0 }, { 0, 1 }, { 1, 1 }, { 1, 2 } };
        StampPattern(engine, cells, x, y);
    }

    /// <summary>
    /// Acorn — takes 5206 generations to stabilize from just 7 cells.
    /// </summary>
    public static void ApplyAcorn(IAutomatonEngine engine, int? offsetX = null, int? offsetY = null)
    {
        int x = offsetX ?? engine.Width / 2 - 3;
        int y = offsetY ?? engine.Height / 2 - 1;

        // .#.....
        // ...#...
        // ##..###
        int[,] cells = { { 1, 0 }, { 3, 1 }, { 0, 2 }, { 1, 2 }, { 4, 2 }, { 5, 2 }, { 6, 2 } };
        StampPattern(engine, cells, x, y);
    }

    private static void StampPattern(IAutomatonEngine engine, int[,] cells, int offsetX, int offsetY)
    {
        for (int i = 0; i < cells.GetLength(0); i++)
        {
            int x = cells[i, 0] + offsetX;
            int y = cells[i, 1] + offsetY;

            // Silently skip cells that fall outside the grid
            if (x >= 0 && x < engine.Width && y >= 0 && y < engine.Height)
                engine.SetCell(x, y, true);
        }
    }
}
