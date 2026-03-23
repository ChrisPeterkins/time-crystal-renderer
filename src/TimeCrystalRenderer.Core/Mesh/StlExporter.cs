using System.Text;

namespace TimeCrystalRenderer.Core.Mesh;

/// <summary>
/// Exports a TriangleMesh as a binary STL file (50 bytes per triangle).
/// </summary>
public static class StlExporter
{
    public static void Export(TriangleMesh mesh, string filePath)
    {
        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export an empty mesh.");

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        // 80-byte header
        var header = new byte[80];
        Encoding.ASCII.GetBytes("TimeCrystalRenderer STL Export", header);
        writer.Write(header);

        // Triangle count
        writer.Write((uint)mesh.TriangleCount);

        // Each triangle: normal (3 floats) + 3 vertices (9 floats) + attribute (uint16)
        var vertices = mesh.Vertices;
        var indices = mesh.Indices;

        for (int i = 0; i < indices.Length; i += 3)
        {
            var v0 = vertices[(int)indices[i]];
            var v1 = vertices[(int)indices[i + 1]];
            var v2 = vertices[(int)indices[i + 2]];

            // Normal
            writer.Write(v0.Normal.X);
            writer.Write(v0.Normal.Y);
            writer.Write(v0.Normal.Z);

            // Vertex 0
            writer.Write(v0.Position.X);
            writer.Write(v0.Position.Y);
            writer.Write(v0.Position.Z);

            // Vertex 1
            writer.Write(v1.Position.X);
            writer.Write(v1.Position.Y);
            writer.Write(v1.Position.Z);

            // Vertex 2
            writer.Write(v2.Position.X);
            writer.Write(v2.Position.Y);
            writer.Write(v2.Position.Z);

            // Attribute byte count (unused, must be 0)
            writer.Write((ushort)0);
        }
    }
}
