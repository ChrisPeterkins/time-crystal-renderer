using System.Runtime.InteropServices;

namespace TimeCrystalRenderer.Core.Mesh;

/// <summary>
/// An indexed triangle mesh built from MeshVertex data.
/// </summary>
public sealed class TriangleMesh
{
    private readonly List<MeshVertex> _vertices;
    private readonly List<uint> _indices;

    public ReadOnlySpan<MeshVertex> Vertices => CollectionsMarshal.AsSpan(_vertices);
    public ReadOnlySpan<uint> Indices => CollectionsMarshal.AsSpan(_indices);

    public int VertexCount => _vertices.Count;
    public int IndexCount => _indices.Count;
    public int TriangleCount => _indices.Count / 3;

    public TriangleMesh(int estimatedTriangles = 1024)
    {
        _vertices = new List<MeshVertex>(estimatedTriangles * 3);
        _indices = new List<uint>(estimatedTriangles * 3);
    }

    /// <summary>
    /// Adds a triangle as three separate vertices with a shared face normal.
    /// </summary>
    public void AddTriangle(MeshVertex v0, MeshVertex v1, MeshVertex v2)
    {
        uint baseIndex = (uint)_vertices.Count;

        _vertices.Add(v0);
        _vertices.Add(v1);
        _vertices.Add(v2);

        _indices.Add(baseIndex);
        _indices.Add(baseIndex + 1);
        _indices.Add(baseIndex + 2);
    }
}
