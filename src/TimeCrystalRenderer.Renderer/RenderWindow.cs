using System.Collections.Concurrent;
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
/// Supports progressive rendering and in-viewer regeneration via keyboard shortcuts.
/// </summary>
public sealed class RenderWindow : IDisposable
{
    private TriangleMesh _mesh;
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
    private int _currentGeneration;
    private bool _cameraInitialized;
    private string _statusMessage = "";
    private double _statusTimer;

    // Thread-safe queue for mesh updates from the background generator
    private readonly ConcurrentQueue<(TriangleMesh Mesh, int Generation)> _pendingMeshUpdates = new();

    /// <summary>
    /// Fired when the user presses a regeneration key (1-5, T, R).
    /// The handler should generate a new mesh and call QueueMeshUpdate.
    /// </summary>
    public event Action<Key>? RegenerationRequested;

    public RenderWindow(TriangleMesh mesh, string stlPath = "time_crystal.stl",
                        string objPath = "time_crystal.obj")
    {
        _mesh = mesh;
        _stlPath = stlPath;
        _objPath = objPath;
    }

    /// <summary>
    /// Queues a new mesh to be uploaded to the GPU on the next render frame.
    /// Safe to call from any thread.
    /// </summary>
    public void QueueMeshUpdate(TriangleMesh mesh, int generation)
    {
        _pendingMeshUpdates.Enqueue((mesh, generation));
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

        _meshRenderer = new MeshRenderer(_gl, shader);

        // Upload initial mesh if provided
        if (_mesh.VertexCount > 0)
        {
            _meshRenderer.Upload(_mesh);
            InitializeCamera();
        }

        // Wire up input
        _camera ??= new OrbitCamera { Distance = 100f };
        var input = _window!.CreateInput();
        _inputHandler = new InputHandler(_camera);
        _inputHandler.RegisterCallbacks(input);
        _inputHandler.KeyPressed += OnKeyPressed;

        PrintControls();
    }

    private static void PrintControls()
    {
        Console.WriteLine("Viewer controls:");
        Console.WriteLine("  Left-drag:   Rotate");
        Console.WriteLine("  Scroll:      Zoom");
        Console.WriteLine("  Middle-drag: Pan");
        Console.WriteLine();
        Console.WriteLine("  1: R-pentomino    2: Glider Gun    3: Acorn");
        Console.WriteLine("  4: Random         5: Glider");
        Console.WriteLine("  T: Toggle smooth  R: Reload (same pattern)");
        Console.WriteLine();
        Console.WriteLine("  S: Save STL       O: Save OBJ");
        Console.WriteLine("  Escape: Quit");
    }

    private void OnKeyPressed(Key key)
    {
        switch (key)
        {
            case Key.S:
                if (_mesh.TriangleCount > 0)
                {
                    StlExporter.Export(_mesh, _stlPath);
                    ShowStatus($"Saved STL: {_stlPath}");
                }
                break;

            case Key.O:
                if (_mesh.TriangleCount > 0)
                {
                    ObjExporter.Export(_mesh, _objPath);
                    ShowStatus($"Saved OBJ: {_objPath}");
                }
                break;

            case Key.Escape:
                _window?.Close();
                break;

            // Pattern and toggle keys — delegate to the host application
            case Key.Number1:
            case Key.Number2:
            case Key.Number3:
            case Key.Number4:
            case Key.Number5:
            case Key.T:
            case Key.R:
                _cameraInitialized = false;
                RegenerationRequested?.Invoke(key);
                break;
        }
    }

    private void OnRender(double deltaTime)
    {
        // Process any pending mesh updates from the background thread
        ProcessMeshUpdates();

        _gl!.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_meshRenderer!.HasMesh)
        {
            float aspectRatio = (float)_window!.Size.X / _window.Size.Y;
            var model = Matrix4x4.Identity;
            var view = _camera!.GetViewMatrix();
            var projection = _camera.GetProjectionMatrix(aspectRatio);

            // Light coming from upper-right-front
            var lightDirection = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.3f));

            _meshRenderer.Draw(model, view, projection, lightDirection, _camera.Position);
        }

        UpdateTitleBar(deltaTime);
    }

    private void ProcessMeshUpdates()
    {
        // Drain the queue, keeping only the latest update
        (TriangleMesh Mesh, int Generation)? latest = null;
        while (_pendingMeshUpdates.TryDequeue(out var update))
            latest = update;

        if (latest.HasValue)
        {
            _mesh = latest.Value.Mesh;
            _currentGeneration = latest.Value.Generation;
            _meshRenderer!.Upload(_mesh);

            if (!_cameraInitialized)
                InitializeCamera();
        }
    }

    private void InitializeCamera()
    {
        _camera = new OrbitCamera
        {
            Target = CenterOfMesh(),
            Distance = MaxMeshExtent() * 1.8f,
            Yaw = MathF.PI / 4f,
            Pitch = MathF.PI / 6f,
        };

        _inputHandler?.SetCamera(_camera);
        _cameraInitialized = true;
    }

    private void ShowStatus(string message)
    {
        _statusMessage = message;
        _statusTimer = 3.0;
        Console.WriteLine(message);
    }

    private void UpdateTitleBar(double deltaTime)
    {
        _frameCount++;
        _fpsTimer += deltaTime;

        if (_statusTimer > 0)
            _statusTimer -= deltaTime;

        if (_fpsTimer >= 1.0)
        {
            _displayFps = _frameCount;
            _frameCount = 0;
            _fpsTimer = 0;

            string genInfo = _currentGeneration > 0 ? $" | Gen {_currentGeneration}" : "";
            string status = _statusTimer > 0 ? $" | {_statusMessage}" : "";
            _window!.Title = $"Time Crystal Renderer | {_mesh.TriangleCount:N0} tris{genInfo} | {_displayFps} FPS{status}";
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
