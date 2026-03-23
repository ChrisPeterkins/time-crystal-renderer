using System.Globalization;

namespace TimeCrystalRenderer.Core.Mesh;

/// <summary>
/// Exports a TriangleMesh as a Wavefront OBJ file with per-vertex colors.
/// Uses the non-standard "v x y z r g b" extension supported by MeshLab and Blender.
/// </summary>
public static class ObjExporter
{
    public static void Export(TriangleMesh mesh, string filePath)
    {
        if (mesh.TriangleCount == 0)
            throw new InvalidOperationException("Cannot export an empty mesh.");

        using var writer = new StreamWriter(filePath);

        writer.WriteLine("# Time Crystal Renderer OBJ Export");
        writer.WriteLine($"# Vertices: {mesh.VertexCount}  Triangles: {mesh.TriangleCount}");
        writer.WriteLine();

        var vertices = mesh.Vertices;
        var indices = mesh.Indices;

        // Vertices with RGB color (v x y z r g b)
        for (int i = 0; i < vertices.Length; i++)
        {
            var v = vertices[i];
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "v {0:F6} {1:F6} {2:F6} {3:F4} {4:F4} {5:F4}",
                v.Position.X, v.Position.Y, v.Position.Z,
                v.Color.X, v.Color.Y, v.Color.Z));
        }

        writer.WriteLine();

        // Normals
        for (int i = 0; i < vertices.Length; i++)
        {
            var n = vertices[i].Normal;
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "vn {0:F6} {1:F6} {2:F6}",
                n.X, n.Y, n.Z));
        }

        writer.WriteLine();

        // Faces (1-indexed)
        for (int i = 0; i < indices.Length; i += 3)
        {
            uint a = indices[i] + 1;
            uint b = indices[i + 1] + 1;
            uint c = indices[i + 2] + 1;
            writer.WriteLine($"f {a}//{a} {b}//{b} {c}//{c}");
        }
    }
}
