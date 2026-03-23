using System.Numerics;
using Silk.NET.OpenGL;

namespace TimeCrystalRenderer.Renderer.Shaders;

/// <summary>
/// Compiles, links, and manages a GLSL shader program (vertex + fragment).
/// </summary>
public sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;

    public ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _handle = _gl.CreateProgram();
        _gl.AttachShader(_handle, vertexShader);
        _gl.AttachShader(_handle, fragmentShader);
        _gl.LinkProgram(_handle);

        _gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            string log = _gl.GetProgramInfoLog(_handle);
            throw new InvalidOperationException($"Shader program link failed:\n{log}");
        }

        // Shaders are linked into the program; originals no longer needed
        _gl.DetachShader(_handle, vertexShader);
        _gl.DetachShader(_handle, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    public void Use()
    {
        _gl.UseProgram(_handle);
    }

    public void SetUniform(string name, Matrix4x4 value)
    {
        int location = GetUniformLocation(name);
        unsafe
        {
            _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }
    }

    public void SetUniform(string name, Vector3 value)
    {
        int location = GetUniformLocation(name);
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    public void SetUniform(string name, Vector4 value)
    {
        int location = GetUniformLocation(name);
        _gl.Uniform4(location, value.X, value.Y, value.Z, value.W);
    }

    public void Dispose()
    {
        _gl.DeleteProgram(_handle);
    }

    private int GetUniformLocation(string name)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
            throw new InvalidOperationException($"Uniform '{name}' not found in shader program.");
        return location;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);
        if (compileStatus == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            throw new InvalidOperationException($"{type} compilation failed:\n{log}");
        }

        return shader;
    }
}
