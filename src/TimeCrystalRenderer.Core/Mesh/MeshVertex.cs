using System.Numerics;
using System.Runtime.InteropServices;

namespace TimeCrystalRenderer.Core.Mesh;

/// <summary>
/// A single vertex with position, normal, and color. Tightly packed for direct GPU upload.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MeshVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Color;

    /// <summary>
    /// Total size in bytes (36). Used for OpenGL vertex attribute stride.
    /// </summary>
    public const int SizeInBytes = 36;

    public MeshVertex(Vector3 position, Vector3 normal, Vector3 color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }
}
