using System.Numerics;
using Silk.NET.OpenGL;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Renderer.Shaders;

namespace TimeCrystalRenderer.Renderer.Rendering;

/// <summary>
/// Uploads a TriangleMesh to the GPU and draws it with Blinn-Phong lighting.
/// </summary>
public sealed class MeshRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private uint _indexCount;

    public MeshRenderer(GL gl, ShaderProgram shader)
    {
        _gl = gl;
        _shader = shader;
    }

    public bool HasMesh => _indexCount > 0;

    /// <summary>
    /// Uploads mesh vertex and index data to GPU buffers.
    /// Can be called multiple times to replace the current mesh (for progressive rendering).
    /// </summary>
    public unsafe void Upload(TriangleMesh mesh)
    {
        // Clean up previous buffers if re-uploading
        if (_vao != 0)
        {
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
        }

        _indexCount = (uint)mesh.IndexCount;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // Vertex buffer
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        var vertices = mesh.Vertices;
        fixed (MeshVertex* vertexPtr = vertices)
        {
            nuint vertexSize = (nuint)(vertices.Length * MeshVertex.SizeInBytes);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, vertexSize, vertexPtr, BufferUsageARB.StaticDraw);
        }

        // Index buffer
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

        var indices = mesh.Indices;
        fixed (uint* indexPtr = indices)
        {
            nuint indexSize = (nuint)(indices.Length * sizeof(uint));
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, indexSize, indexPtr, BufferUsageARB.StaticDraw);
        }

        // Vertex attributes: position (0), normal (1), color (2)
        // All are vec3 (3 floats), stride = 36 bytes (MeshVertex.SizeInBytes)
        uint stride = MeshVertex.SizeInBytes;

        // Position at offset 0
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

        // Normal at offset 12 (after 3 floats for position)
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)12);

        // Color at offset 24 (after 3 floats for position + 3 for normal)
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)24);

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws the mesh with the given camera and lighting parameters.
    /// </summary>
    public unsafe void Draw(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection,
                            Vector3 lightDirection, Vector3 cameraPosition)
    {
        _shader.Use();
        _shader.SetUniform("uModel", model);
        _shader.SetUniform("uView", view);
        _shader.SetUniform("uProjection", projection);
        _shader.SetUniform("uLightDir", lightDirection);
        _shader.SetUniform("uViewPos", cameraPosition);

        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
        _shader.Dispose();
    }
}
