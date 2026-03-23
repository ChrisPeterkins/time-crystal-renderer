namespace TimeCrystalRenderer.Core.Automata;

/// <summary>
/// Defines a 2D cellular automaton that advances one generation at a time.
/// </summary>
public interface IAutomatonEngine
{
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Returns the current grid state as a flat row-major array. 1 = alive, 0 = dead.
    /// </summary>
    ReadOnlySpan<byte> CurrentState { get; }

    void SetCell(int x, int y, bool alive);
    bool GetCell(int x, int y);

    /// <summary>
    /// Advances the simulation by one generation.
    /// </summary>
    void Step();

    void Clear();
}
