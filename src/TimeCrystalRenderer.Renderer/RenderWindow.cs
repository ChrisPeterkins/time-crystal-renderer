using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using TimeCrystalRenderer.Core.Mesh;
using TimeCrystalRenderer.Renderer.Input;
using TimeCrystalRenderer.Renderer.Rendering;
using TimeCrystalRenderer.Renderer.Shaders;

namespace TimeCrystalRenderer.Renderer;

/// <summary>
/// Creates an OpenGL window and renders a TriangleMesh with orbit camera controls.
/// </summary>
public sealed class RenderWindow : IDisposable
{
    private readonly TriangleMesh _mesh;
    private readonly string _stlPath;
    private readonly string _objPath;

    private IWindow? _window;
    private GL? _gl;
    private MeshRenderer? _meshRenderer;
    private OrbitCamera? _camera;
    private InputHandler? _inputHandler;
    private int _frameCount;
    private double _fpsTimer;
    private int _displayFps;

    public RenderWindow(TriangleMesh mesh, string stlPath = "time_crystal.stl",
                        string objPath = "time_crystal.obj")
    {
        _mesh = mesh;
        _stlPath = stlPath;
        _objPath = objPath;
    }

    public void Run()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1280, 800),
            Title = "Time Crystal Renderer",
            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core,
                                  ContextFlags.Default, new APIVersion(4, 1))
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.Closing += OnClosing;
        _window.Run();
    }

    private void OnLoad()
    {
        _gl = GL.GetApi(_window!);

        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.ClearColor(0.08f, 0.08f, 0.1f, 1f);

        // Load shaders from files next to the executable
        string vertexSource = File.ReadAllText(ShaderPath("mesh.vert"));
        string fragmentSource = File.ReadAllText(ShaderPath("mesh.frag"));
        var shader = new ShaderProgram(_gl, vertexSource, fragmentSource);

        // Upload mesh to GPU
        _meshRenderer = new MeshRenderer(_gl, shader);
        _meshRenderer.Upload(_mesh);

        // Position camera to see the entire crystal
        _camera = new OrbitCamera
        {
            Target = CenterOfMesh(),
            Distance = MaxMeshExtent() * 1.8f,
            Yaw = MathF.PI / 4f,
            Pitch = MathF.PI / 6f,
        };

        // Wire up input
        var input = _window!.CreateInput();
        _inputHandler = new InputHandler(_camera);
        _inputHandler.RegisterCallbacks(input);
        _inputHandler.KeyPressed += OnKeyPressed;

        Console.WriteLine("Viewer controls:");
        Console.WriteLine("  Left-drag:   Rotate");
        Console.WriteLine("  Scroll:      Zoom");
        Console.WriteLine("  Middle-drag: Pan");
        Console.WriteLine("  S:           Save STL");
        Console.WriteLine("  O:           Save OBJ");
        Console.WriteLine("  Escape:      Quit");
    }

    private void OnKeyPressed(Key key)
    {
        switch (key)
        {
            case Key.S:
                StlExporter.Export(_mesh, _stlPath);
                Console.WriteLine($"Saved STL: {_stlPath}");
                break;

            case Key.O:
                ObjExporter.Export(_mesh, _objPath);
                Console.WriteLine($"Saved OBJ: {_objPath}");
                break;

            case Key.Escape:
                _window?.Close();
                break;
        }
    }

    private void OnRender(double deltaTime)
    {
        _gl!.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        float aspectRatio = (float)_window!.Size.X / _window.Size.Y;
        var model = Matrix4x4.Identity;
        var view = _camera!.GetViewMatrix();
        var projection = _camera.GetProjectionMatrix(aspectRatio);

        // Light coming from upper-right-front
        var lightDirection = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.3f));

        _meshRenderer!.Draw(model, view, projection, lightDirection, _camera.Position);

        UpdateFpsCounter(deltaTime);
    }

    private void UpdateFpsCounter(double deltaTime)
    {
        _frameCount++;
        _fpsTimer += deltaTime;

        if (_fpsTimer >= 1.0)
        {
            _displayFps = _frameCount;
            _frameCount = 0;
            _fpsTimer = 0;

            _window!.Title = $"Time Crystal Renderer | {_mesh.TriangleCount:N0} tris | {_displayFps} FPS";
        }
    }

    private void OnResize(Vector2D<int> size)
    {
        _gl?.Viewport(size);
    }

    private void OnClosing()
    {
        _meshRenderer?.Dispose();
    }

    public void Dispose()
    {
        _window?.Dispose();
    }

    /// <summary>
    /// Computes the center of the mesh bounding box for camera targeting.
    /// </summary>
    private Vector3 CenterOfMesh()
    {
        var vertices = _mesh.Vertices;
        if (vertices.Length == 0)
            return Vector3.Zero;

        var min = vertices[0].Position;
        var max = vertices[0].Position;

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i].Position);
            max = Vector3.Max(max, vertices[i].Position);
        }

        return (min + max) * 0.5f;
    }

    /// <summary>
    /// Returns the largest extent of the mesh bounding box for initial camera distance.
    /// </summary>
    private float MaxMeshExtent()
    {
        var vertices = _mesh.Vertices;
        if (vertices.Length == 0)
            return 100f;

        var min = vertices[0].Position;
        var max = vertices[0].Position;

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i].Position);
            max = Vector3.Max(max, vertices[i].Position);
        }

        var extent = max - min;
        return MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));
    }

    private static string ShaderPath(string fileName)
    {
        string baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Shaders", fileName);
    }
}
