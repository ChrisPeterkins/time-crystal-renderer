using System.Numerics;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Core.Voxel;

namespace TimeCrystalRenderer.Core.MarchingCubes;

/// <summary>
/// Extracts an isosurface from a voxel volume using the Marching Cubes algorithm.
/// Each 2x2x2 cell of voxels is classified, and triangles are generated from lookup tables.
/// </summary>
public sealed class MarchingCubesExtractor
{
    private readonly float _isoLevel;

    public MarchingCubesExtractor(float isoLevel = 0.5f)
    {
        _isoLevel = isoLevel;
    }

    /// <summary>
    /// Extracts a triangle mesh from the voxel volume (binary sampling).
    /// The colorMapper converts a Z (time) coordinate into an RGB color.
    /// </summary>
    public TriangleMesh Extract(VoxelVolume volume, Func<float, float, Vector3> colorMapper,
                                IProgress<int>? progress = null)
    {
        return Extract(volume.SizeX, volume.SizeY, volume.SizeZ,
                       volume.Sample, colorMapper, progress);
    }

    /// <summary>
    /// Extracts a triangle mesh from a smoothed scalar field.
    /// The sampler function returns a float value at each (x, y, z) coordinate.
    /// </summary>
    public TriangleMesh Extract(int sizeX, int sizeY, int sizeZ,
                                Func<int, int, int, float> sampler,
                                Func<float, float, Vector3> colorMapper,
                                IProgress<int>? progress = null)
    {
        long totalVoxels = (long)sizeX * sizeY * sizeZ;
        int estimatedTriangles = (int)(totalVoxels / 10);
        var mesh = new TriangleMesh(Math.Max(estimatedTriangles, 1024));

        // Iterate every cube in the volume. Each cube spans from (x,y,z) to (x+1,y+1,z+1).
        for (int z = 0; z < sizeZ - 1; z++)
        {
            for (int y = 0; y < sizeY - 1; y++)
            {
                for (int x = 0; x < sizeX - 1; x++)
                {
                    ProcessCube(sampler, mesh, x, y, z, sizeZ, colorMapper);
                }
            }

            progress?.Report(z + 1);
        }

        return mesh;
    }

    private void ProcessCube(Func<int, int, int, float> sampler, TriangleMesh mesh,
                             int x, int y, int z, int sizeZ,
                             Func<float, float, Vector3> colorMapper)
    {
        // Step 1: Sample the 8 corners of this cube
        //
        // Corner numbering (Bourke convention):
        //     4------5        Y
        //    /|     /|        |
        //   7------6 |        +-- X
        //   | 0----|-1       /
        //   |/     |/       Z
        //   3------2
        Span<float> cornerValues = stackalloc float[8];
        cornerValues[0] = sampler(x,     y,     z);
        cornerValues[1] = sampler(x + 1, y,     z);
        cornerValues[2] = sampler(x + 1, y,     z + 1);
        cornerValues[3] = sampler(x,     y,     z + 1);
        cornerValues[4] = sampler(x,     y + 1, z);
        cornerValues[5] = sampler(x + 1, y + 1, z);
        cornerValues[6] = sampler(x + 1, y + 1, z + 1);
        cornerValues[7] = sampler(x,     y + 1, z + 1);

        // Step 2: Build the cube index from which corners are inside the surface
        int cubeIndex = 0;
        for (int i = 0; i < 8; i++)
        {
            if (cornerValues[i] >= _isoLevel)
                cubeIndex |= 1 << i;
        }

        // No triangles if the cube is entirely inside or outside
        int edgeMask = MarchingCubesLookupTables.EdgeTable[cubeIndex];
        if (edgeMask == 0)
            return;

        // Step 3: Compute corner positions in world space
        Span<Vector3> cornerPositions = stackalloc Vector3[8];
        cornerPositions[0] = new Vector3(x,     y,     z);
        cornerPositions[1] = new Vector3(x + 1, y,     z);
        cornerPositions[2] = new Vector3(x + 1, y,     z + 1);
        cornerPositions[3] = new Vector3(x,     y,     z + 1);
        cornerPositions[4] = new Vector3(x,     y + 1, z);
        cornerPositions[5] = new Vector3(x + 1, y + 1, z);
        cornerPositions[6] = new Vector3(x + 1, y + 1, z + 1);
        cornerPositions[7] = new Vector3(x,     y + 1, z + 1);

        // Step 4: Interpolate vertex positions along the 12 edges
        // Each edge connects two corners. We find where the isosurface crosses.
        Span<Vector3> edgeVertices = stackalloc Vector3[12];

        int[,] edgeConnections = EdgeConnections;
        for (int i = 0; i < 12; i++)
        {
            if ((edgeMask & (1 << i)) != 0)
            {
                int a = edgeConnections[i, 0];
                int b = edgeConnections[i, 1];
                edgeVertices[i] = Interpolate(
                    cornerPositions[a], cornerPositions[b],
                    cornerValues[a], cornerValues[b]);
            }
        }

        // Step 5: Emit triangles from the lookup table
        float maxZ = sizeZ - 1;
        for (int i = 0; MarchingCubesLookupTables.TriTable[cubeIndex, i] != -1; i += 3)
        {
            Vector3 p0 = edgeVertices[MarchingCubesLookupTables.TriTable[cubeIndex, i]];
            Vector3 p1 = edgeVertices[MarchingCubesLookupTables.TriTable[cubeIndex, i + 1]];
            Vector3 p2 = edgeVertices[MarchingCubesLookupTables.TriTable[cubeIndex, i + 2]];

            // Face normal from cross product
            Vector3 normal = Vector3.Normalize(Vector3.Cross(p1 - p0, p2 - p0));

            // Color based on average Z position (time axis)
            float avgZ = (p0.Z + p1.Z + p2.Z) / 3f;
            Vector3 color = colorMapper(avgZ, maxZ);

            mesh.AddTriangle(
                new MeshVertex(p0, normal, color),
                new MeshVertex(p1, normal, color),
                new MeshVertex(p2, normal, color));
        }
    }

    private Vector3 Interpolate(Vector3 positionA, Vector3 positionB,
                                float valueA, float valueB)
    {
        // For binary voxel data (0 or 1), this places the vertex at the midpoint
        if (MathF.Abs(valueA - valueB) < 1e-6f)
            return positionA;

        float t = (_isoLevel - valueA) / (valueB - valueA);
        return Vector3.Lerp(positionA, positionB, t);
    }

    /// <summary>
    /// Maps each of the 12 edges to the pair of corner indices it connects.
    /// </summary>
    private static readonly int[,] EdgeConnections =
    {
        { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },  // Bottom face edges 0-3
        { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },  // Top face edges 4-7
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },  // Vertical edges 8-11
    };
}
