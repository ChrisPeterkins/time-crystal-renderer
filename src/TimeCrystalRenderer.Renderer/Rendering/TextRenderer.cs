using System.Numerics;
using Silk.NET.OpenGL;
using TimeCrystalRenderer.Renderer.Shaders;

namespace TimeCrystalRenderer.Renderer.Rendering;

/// <summary>
/// Renders text strings on screen using the built-in 8x8 bitmap font.
/// Uses an orthographic projection for pixel-perfect 2D overlay rendering.
/// </summary>
public sealed class TextRenderer : IDisposable
{
    private const int MaxCharsPerBatch = 1024;
    private const int VerticesPerChar = 6; // Two triangles per character quad
    private const int FloatsPerVertex = 4; // x, y, u, v

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _fontTexture;
    private readonly float[] _vertexBuffer = new float[MaxCharsPerBatch * VerticesPerChar * FloatsPerVertex];

    public TextRenderer(GL gl, string shaderDirectory)
    {
        _gl = gl;

        string vertexSource = File.ReadAllText(Path.Combine(shaderDirectory, "text.vert"));
        string fragmentSource = File.ReadAllText(Path.Combine(shaderDirectory, "text.frag"));
        _shader = new ShaderProgram(gl, vertexSource, fragmentSource);

        _fontTexture = CreateFontTexture();

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        // Allocate buffer space (will be filled per-frame)
        nuint bufferSize = (nuint)(_vertexBuffer.Length * sizeof(float));
        unsafe { _gl.BufferData(BufferTargetARB.ArrayBuffer, bufferSize, null, BufferUsageARB.DynamicDraw); }

        uint stride = FloatsPerVertex * sizeof(float);

        // Position (vec2) at offset 0
        _gl.EnableVertexAttribArray(0);
        unsafe { _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0); }

        // TexCoord (vec2) at offset 8
        _gl.EnableVertexAttribArray(1);
        unsafe { _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)8); }

        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws a block of text lines at the given screen position.
    /// </summary>
    public void DrawText(string[] lines, float startX, float startY,
                         float scale, Vector4 color, int screenWidth, int screenHeight)
    {
        _shader.Use();

        // Orthographic projection: (0,0) = top-left, (width, height) = bottom-right
        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0, screenWidth, screenHeight, 0, -1, 1);
        _shader.SetUniform("uProjection", projection);
        _shader.SetUniform("uColor", color);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _fontTexture);

        int totalChars = 0;
        float charWidth = BitmapFont.CharWidth * scale;
        float charHeight = BitmapFont.CharHeight * scale;
        float atlasCharWidth = 1f / BitmapFont.CharCount;

        float cursorY = startY;
        foreach (string line in lines)
        {
            float cursorX = startX;
            foreach (char character in line)
            {
                if (totalChars >= MaxCharsPerBatch)
                    break;

                int charIndex = character - BitmapFont.FirstChar;
                if (charIndex < 0 || charIndex >= BitmapFont.CharCount)
                    charIndex = 0; // Fallback to space

                float uvLeft = charIndex * atlasCharWidth;
                float uvRight = (charIndex + 1) * atlasCharWidth;

                // Emit two triangles for this character quad
                int offset = totalChars * VerticesPerChar * FloatsPerVertex;
                EmitQuad(_vertexBuffer, offset,
                         cursorX, cursorY, cursorX + charWidth, cursorY + charHeight,
                         uvLeft, 0f, uvRight, 1f);

                cursorX += charWidth;
                totalChars++;
            }
            cursorY += charHeight + 2 * scale;
        }

        if (totalChars == 0)
            return;

        // Upload vertex data and draw
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        int dataSize = totalChars * VerticesPerChar * FloatsPerVertex;
        unsafe
        {
            fixed (float* ptr = _vertexBuffer)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                                  (nuint)(dataSize * sizeof(float)), ptr);
            }
        }

        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(totalChars * VerticesPerChar));
        _gl.BindVertexArray(0);
    }

    /// <summary>
    /// Draws a filled rectangle as a background panel.
    /// </summary>
    public void DrawPanel(float x, float y, float width, float height,
                          Vector4 color, int screenWidth, int screenHeight)
    {
        _shader.Use();

        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0, screenWidth, screenHeight, 0, -1, 1);
        _shader.SetUniform("uProjection", projection);
        _shader.SetUniform("uColor", color);

        // Use a solid white region of the font texture (any filled pixel works)
        // Character 'M' at row 0 has solid pixels — use the top-left corner
        float uvSolid = (('M' - BitmapFont.FirstChar) * 1f / BitmapFont.CharCount) + 0.001f;

        EmitQuad(_vertexBuffer, 0, x, y, x + width, y + height,
                 uvSolid, 0.1f, uvSolid + 0.001f, 0.2f);

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* ptr = _vertexBuffer)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                                  (nuint)(VerticesPerChar * FloatsPerVertex * sizeof(float)), ptr);
            }
        }

        _gl.DrawArrays(PrimitiveType.Triangles, 0, VerticesPerChar);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_fontTexture);
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _shader.Dispose();
    }

    private uint CreateFontTexture()
    {
        var atlas = BitmapFont.BuildAtlas(out int atlasWidth, out int atlasHeight);
        uint texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);

        unsafe
        {
            fixed (byte* ptr = atlas)
            {
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8,
                               (uint)atlasWidth, (uint)atlasHeight, 0,
                               PixelFormat.Red, PixelType.UnsignedByte, ptr);
            }
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

        return texture;
    }

    /// <summary>
    /// Writes 6 vertices (2 triangles) for a textured quad into the buffer.
    /// </summary>
    private static void EmitQuad(float[] buffer, int offset,
                                 float left, float top, float right, float bottom,
                                 float uvLeft, float uvTop, float uvRight, float uvBottom)
    {
        // Triangle 1: top-left, bottom-left, bottom-right
        buffer[offset]      = left;  buffer[offset + 1]  = top;    buffer[offset + 2]  = uvLeft;  buffer[offset + 3]  = uvTop;
        buffer[offset + 4]  = left;  buffer[offset + 5]  = bottom; buffer[offset + 6]  = uvLeft;  buffer[offset + 7]  = uvBottom;
        buffer[offset + 8]  = right; buffer[offset + 9]  = bottom; buffer[offset + 10] = uvRight; buffer[offset + 11] = uvBottom;

        // Triangle 2: top-left, bottom-right, top-right
        buffer[offset + 12] = left;  buffer[offset + 13] = top;    buffer[offset + 14] = uvLeft;  buffer[offset + 15] = uvTop;
        buffer[offset + 16] = right; buffer[offset + 17] = bottom; buffer[offset + 18] = uvRight; buffer[offset + 19] = uvBottom;
        buffer[offset + 20] = right; buffer[offset + 21] = top;    buffer[offset + 22] = uvRight; buffer[offset + 23] = uvTop;
    }
}
