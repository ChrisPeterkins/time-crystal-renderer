using System.Numerics;

namespace TimeCrystalRenderer.Core;

/// <summary>
/// Maps generation (time) values to colors for the mesh gradient.
/// </summary>
public static class ColorMapper
{
    /// <summary>
    /// Converts a generation index to an RGB color along a warm-to-cool gradient.
    /// Early generations are red/orange, late generations are blue/purple.
    /// </summary>
    public static Vector3 GenerationToColor(float z, float maxZ)
    {
        float t = maxZ > 0 ? z / maxZ : 0f;

        // Hue sweep: 0° (red) → 240° (blue)
        float hue = t * 240f;
        return HsvToRgb(hue, saturation: 0.8f, value: 0.9f);
    }

    private static Vector3 HsvToRgb(float hue, float saturation, float value)
    {
        float c = value * saturation;
        float x = c * (1f - MathF.Abs(hue / 60f % 2f - 1f));
        float m = value - c;

        var (r, g, b) = hue switch
        {
            < 60f  => (c, x, 0f),
            < 120f => (x, c, 0f),
            < 180f => (0f, c, x),
            < 240f => (0f, x, c),
            < 300f => (x, 0f, c),
            _      => (c, 0f, x),
        };

        return new Vector3(r + m, g + m, b + m);
    }
}
