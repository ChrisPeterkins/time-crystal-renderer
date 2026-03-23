namespace TimeCrystalRenderer.Core.Automata;

/// <summary>
/// Conway's Game of Life (B3/S23) on a toroidal grid.
/// </summary>
public sealed class GameOfLifeEngine : IAutomatonEngine
{
    private byte[] _current;
    private byte[] _next;

    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> CurrentState => _current;

    public GameOfLifeEngine(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        _current = new byte[width * height];
        _next = new byte[width * height];
    }

    public void SetCell(int x, int y, bool alive)
    {
        ValidateCoordinates(x, y);
        _current[y * Width + x] = alive ? (byte)1 : (byte)0;
    }

    public bool GetCell(int x, int y)
    {
        ValidateCoordinates(x, y);
        return _current[y * Width + x] != 0;
    }

    public void Step()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int neighbors = CountNeighbors(x, y);
                bool isAlive = _current[y * Width + x] != 0;

                // B3/S23: born if exactly 3 neighbors, survive if 2 or 3 neighbors
                bool willLive = isAlive
                    ? neighbors is 2 or 3
                    : neighbors is 3;

                _next[y * Width + x] = willLive ? (byte)1 : (byte)0;
            }
        }

        // Swap buffers instead of copying
        (_current, _next) = (_next, _current);
    }

    public void Clear()
    {
        Array.Clear(_current);
        Array.Clear(_next);
    }

    private int CountNeighbors(int centerX, int centerY)
    {
        int count = 0;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                // Wrap around for toroidal topology
                int nx = (centerX + dx + Width) % Width;
                int ny = (centerY + dy + Height) % Height;

                count += _current[ny * Width + nx];
            }
        }

        return count;
    }

    private void ValidateCoordinates(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), $"Must be 0..{Width - 1}, got {x}");
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), $"Must be 0..{Height - 1}, got {y}");
    }
}
