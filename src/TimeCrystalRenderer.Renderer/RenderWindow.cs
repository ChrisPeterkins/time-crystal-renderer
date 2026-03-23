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
    private const int DefaultWindowWidth = 1280;
    private const int DefaultWindowHeight = 800;
    private const float CameraDistanceMultiplier = 1.8f;
    private const double StatusDisplaySeconds = 3.0;

    private static readonly Vector3 DefaultLightDirection = Vector3.Normalize(new Vector3(-0.5f, -1f, -0.3f));

    private TriangleMesh _mesh;
    private readonly string _stlPath;
    private readonly string _objPath;

    private IWindow? _window;
    private GL? _gl;
    private MeshRenderer? _meshRenderer;
    private TextRenderer? _textRenderer;
    private OrbitCamera? _camera;
    private InputHandler? _inputHandler;
    private int _frameCount;
    private double _fpsTimer;
    private int _displayFps;
    private int _currentGeneration;
    private bool _cameraInitialized;
    private bool _isHelpVisible = true;
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
            Size = new Vector2D<int>(DefaultWindowWidth, DefaultWindowHeight),
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
        _textRenderer = new TextRenderer(_gl, ShaderDirectory());

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
        // Controls are printed by the host application (Program.cs)
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

            case Key.H:
                _isHelpVisible = !_isHelpVisible;
                break;

            case Key.Escape:
                _window?.Close();
                break;

            // Pattern, toggle, and mode keys — delegate to the host application
            case Key.Number1:
            case Key.Number2:
            case Key.Number3:
            case Key.Number4:
            case Key.Number5:
            case Key.Number6:
            case Key.T:
            case Key.L:
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

            _meshRenderer.Draw(model, view, projection, DefaultLightDirection, _camera.Position);
        }

        if (_isHelpVisible)
            DrawHelpOverlay();

        UpdateTitleBar(deltaTime);
    }

    private static readonly string[] HelpLines =
    {
        "CAMERA",
        "  Left-drag    Rotate",
        "  Scroll       Zoom",
        "  Middle-drag  Pan",
        "",
        "PATTERNS",
        "  1  R-pentomino",
        "  2  Glider Gun",
        "  3  Acorn",
        "  4  Random (full)",
        "  5  Glider",
        "  6  Soup (central blob)",
        "",
        "SETTINGS",
        "  T  Toggle smooth",
        "  L  Toggle live growth",
        "  R  Reload pattern",
        "",
        "EXPORT",
        "  S  Save STL",
        "  O  Save OBJ",
        "",
        "  H  Hide this help",
        "  Esc  Quit",
    };

    private void DrawHelpOverlay()
    {
        int screenWidth = _window!.Size.X;
        int screenHeight = _window.Size.Y;
        const float scale = 2f;
        const float padding = 16f;
        float lineHeight = BitmapFont.CharHeight * scale + 2 * scale;

        float panelWidth = 26 * BitmapFont.CharWidth * scale + padding * 2;
        float panelHeight = HelpLines.Length * lineHeight + padding * 2;

        // Disable depth test for 2D overlay
        _gl!.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // Semi-transparent background panel
        _textRenderer!.DrawPanel(padding, padding, panelWidth, panelHeight,
                                 new Vector4(0f, 0f, 0f, 0.7f), screenWidth, screenHeight);

        // Help text
        _textRenderer.DrawText(HelpLines, padding * 2, padding * 2, scale,
                               new Vector4(1f, 1f, 1f, 0.9f), screenWidth, screenHeight);

        // Restore 3D rendering state
        _gl.Enable(EnableCap.DepthTest);
        _gl.Enable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);
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
            Distance = MaxMeshExtent() * CameraDistanceMultiplier,
            Yaw = MathF.PI / 4f,
            Pitch = MathF.PI / 6f,
        };

        _inputHandler?.SetCamera(_camera);
        _cameraInitialized = true;
    }

    private void ShowStatus(string message)
    {
        _statusMessage = message;
        _statusTimer = StatusDisplaySeconds;
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
        _textRenderer?.Dispose();
    }

    public void Dispose()
    {
        _window?.Dispose();
    }

    private (Vector3 Min, Vector3 Max) ComputeBounds()
    {
        var vertices = _mesh.Vertices;
        if (vertices.Length == 0)
            return (Vector3.Zero, Vector3.Zero);

        var min = vertices[0].Position;
        var max = vertices[0].Position;

        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i].Position);
            max = Vector3.Max(max, vertices[i].Position);
        }

        return (min, max);
    }

    private Vector3 CenterOfMesh()
    {
        var (min, max) = ComputeBounds();
        return (min + max) * 0.5f;
    }

    private float MaxMeshExtent()
    {
        var (min, max) = ComputeBounds();
        var extent = max - min;
        return MathF.Max(extent.X, MathF.Max(extent.Y, extent.Z));
    }

    private static string ShaderDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Shaders");
    }

    private static string ShaderPath(string fileName)
    {
        return Path.Combine(ShaderDirectory(), fileName);
    }
}
