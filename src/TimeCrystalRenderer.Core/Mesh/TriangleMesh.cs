using System.Numerics;
using System.Runtime.InteropServices;

namespace TimeCrystalRenderer.Core.Mesh;

/// <summary>
/// An indexed triangle mesh built from MeshVertex data.
/// </summary>
public sealed class TriangleMesh
{
    private List<MeshVertex> _vertices;
    private List<uint> _indices;

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

    /// <summary>
    /// Merges vertices that share the same position (within epsilon) and averages their normals.
    /// Reduces vertex count significantly and produces smooth shading across shared edges.
    /// </summary>
    public void DeduplicateAndSmoothNormals(float positionEpsilon = 1e-4f)
    {
        // Step 1: Build a spatial hash to find duplicate vertices efficiently.
        // Key = quantized position, Value = index of the first vertex at that position.
        var positionToIndex = new Dictionary<long, uint>();
        var remappedVertices = new List<MeshVertex>(_vertices.Count / 3);
        var indexRemap = new uint[_vertices.Count];

        for (int i = 0; i < _vertices.Count; i++)
        {
            long key = QuantizePosition(_vertices[i].Position, positionEpsilon);

            if (positionToIndex.TryGetValue(key, out uint existingIndex))
            {
                // Vertex already exists — will merge normals in step 2
                indexRemap[i] = existingIndex;
            }
            else
            {
                uint newIndex = (uint)remappedVertices.Count;
                positionToIndex[key] = newIndex;
                indexRemap[i] = newIndex;
                remappedVertices.Add(_vertices[i]);
            }
        }

        // Step 2: Remap all triangle indices and accumulate face normals at shared vertices.
        // Zero out normals first, then accumulate.
        var normalAccumulator = new Vector3[remappedVertices.Count];
        var remappedIndices = new List<uint>(_indices.Count);

        for (int i = 0; i < _indices.Count; i += 3)
        {
            uint i0 = indexRemap[_indices[i]];
            uint i1 = indexRemap[_indices[i + 1]];
            uint i2 = indexRemap[_indices[i + 2]];

            // Skip degenerate triangles (all three vertices merged to same point)
            if (i0 == i1 || i1 == i2 || i0 == i2)
                continue;

            remappedIndices.Add(i0);
            remappedIndices.Add(i1);
            remappedIndices.Add(i2);

            // Compute face normal and accumulate at each vertex
            var p0 = remappedVertices[(int)i0].Position;
            var p1 = remappedVertices[(int)i1].Position;
            var p2 = remappedVertices[(int)i2].Position;
            var faceNormal = Vector3.Cross(p1 - p0, p2 - p0);

            normalAccumulator[i0] += faceNormal;
            normalAccumulator[i1] += faceNormal;
            normalAccumulator[i2] += faceNormal;
        }

        // Step 3: Normalize the accumulated normals and write back.
        for (int i = 0; i < remappedVertices.Count; i++)
        {
            var vertex = remappedVertices[i];
            var smoothNormal = normalAccumulator[i];

            vertex.Normal = smoothNormal.LengthSquared() > 0
                ? Vector3.Normalize(smoothNormal)
                : vertex.Normal;

            remappedVertices[i] = vertex;
        }

        _vertices = remappedVertices;
        _indices = remappedIndices;
    }

    /// <summary>
    /// Hashes a position into a single long by quantizing each component to a grid.
    /// Vertices within epsilon of each other land in the same bucket.
    /// </summary>
    private static long QuantizePosition(Vector3 position, float epsilon)
    {
        // Quantize to grid cells. Using primes to reduce hash collisions.
        long qx = (long)MathF.Round(position.X / epsilon);
        long qy = (long)MathF.Round(position.Y / epsilon);
        long qz = (long)MathF.Round(position.Z / epsilon);

        // Combine with large primes to distribute across the long range
        return qx * 73856093L ^ qy * 19349663L ^ qz * 83492791L;
    }
}
