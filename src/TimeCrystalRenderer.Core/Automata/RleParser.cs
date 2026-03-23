namespace TimeCrystalRenderer.Core.Automata;

/// <summary>
/// Parses Run Length Encoded (RLE) pattern files used by the Game of Life community.
/// Format: b = dead, o = alive, $ = end of row, ! = end of pattern.
/// Numbers prefix a character to repeat it (e.g. "3o" = "ooo").
/// </summary>
public static class RleParser
{
    /// <summary>
    /// Parses an RLE string and stamps the pattern onto the engine grid.
    /// Centers the pattern on the grid if no offset is provided.
    /// </summary>
    public static void ApplyRle(IAutomatonEngine engine, string rleContent,
                                int? offsetX = null, int? offsetY = null)
    {
        var (patternWidth, patternHeight, rows) = Parse(rleContent);

        int startX = offsetX ?? (engine.Width - patternWidth) / 2;
        int startY = offsetY ?? (engine.Height - patternHeight) / 2;

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            int col = 0;

            foreach (var (count, isAlive) in row)
            {
                if (isAlive)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int x = startX + col + i;
                        int y = startY + rowIndex;

                        if (x >= 0 && x < engine.Width && y >= 0 && y < engine.Height)
                            engine.SetCell(x, y, true);
                    }
                }
                col += count;
            }
        }
    }

    /// <summary>
    /// Loads an RLE file from disk and applies it.
    /// </summary>
    public static void ApplyRleFile(IAutomatonEngine engine, string filePath,
                                    int? offsetX = null, int? offsetY = null)
    {
        string content = File.ReadAllText(filePath);
        ApplyRle(engine, content, offsetX, offsetY);
    }

    private static (int Width, int Height, List<List<(int Count, bool IsAlive)>> Rows) Parse(string rleContent)
    {
        int patternWidth = 0;
        int patternHeight = 0;
        string rleBody = "";

        foreach (string rawLine in rleContent.Split('\n'))
        {
            string line = rawLine.Trim();

            // Skip comments
            if (line.StartsWith('#'))
                continue;

            // Parse header line: "x = 36, y = 9, rule = B3/S23"
            if (line.StartsWith("x", StringComparison.OrdinalIgnoreCase) && line.Contains('='))
            {
                ParseHeader(line, out patternWidth, out patternHeight);
                continue;
            }

            // Accumulate the RLE body (may span multiple lines)
            rleBody += line;

            if (line.Contains('!'))
                break;
        }

        var rows = DecodeRleBody(rleBody);

        // Use parsed rows to determine dimensions if header was missing
        if (patternHeight == 0)
            patternHeight = rows.Count;

        return (patternWidth, patternHeight, rows);
    }

    private static void ParseHeader(string line, out int width, out int height)
    {
        width = 0;
        height = 0;

        foreach (string part in line.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.StartsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                string value = trimmed.Split('=')[1].Trim();
                int.TryParse(value, out width);
            }
            else if (trimmed.StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                string value = trimmed.Split('=')[1].Trim();
                int.TryParse(value, out height);
            }
        }
    }

    private static List<List<(int Count, bool IsAlive)>> DecodeRleBody(string body)
    {
        var rows = new List<List<(int, bool)>>();
        var currentRow = new List<(int, bool)>();
        int runCount = 0;

        foreach (char ch in body)
        {
            if (char.IsDigit(ch))
            {
                runCount = runCount * 10 + (ch - '0');
                continue;
            }

            int count = runCount > 0 ? runCount : 1;
            runCount = 0;

            switch (ch)
            {
                case 'b':
                    currentRow.Add((count, false));
                    break;

                case 'o':
                    currentRow.Add((count, true));
                    break;

                case '$':
                    // End of row — '$' can repeat to skip blank rows
                    rows.Add(currentRow);
                    for (int i = 1; i < count; i++)
                        rows.Add(new List<(int, bool)>());
                    currentRow = new List<(int, bool)>();
                    break;

                case '!':
                    // End of pattern
                    if (currentRow.Count > 0)
                        rows.Add(currentRow);
                    return rows;
            }
        }

        // Handle patterns without trailing '!'
        if (currentRow.Count > 0)
            rows.Add(currentRow);

        return rows;
    }
}
