using Silk.NET.Input;
using TimeCrystalRenderer.Renderer.Rendering;

namespace TimeCrystalRenderer.Renderer.Input;

/// <summary>
/// Wires mouse and keyboard input to camera controls.
/// Left-drag rotates, scroll zooms, middle-drag pans.
/// </summary>
public sealed class InputHandler
{
    private const float RotationSensitivity = 0.005f;
    private const float ZoomSensitivity = 5f;
    private const float PanSensitivity = 0.1f;

    private readonly OrbitCamera _camera;
    private bool _isRotating;
    private bool _isPanning;
    private float _lastMouseX;
    private float _lastMouseY;

    public InputHandler(OrbitCamera camera)
    {
        _camera = camera;
    }

    public void RegisterCallbacks(IInputContext input)
    {
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnScroll;
        }
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            _isRotating = true;
            _lastMouseX = mouse.Position.X;
            _lastMouseY = mouse.Position.Y;
        }
        else if (button == MouseButton.Middle)
        {
            _isPanning = true;
            _lastMouseX = mouse.Position.X;
            _lastMouseY = mouse.Position.Y;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
            _isRotating = false;
        else if (button == MouseButton.Middle)
            _isPanning = false;
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        float deltaX = position.X - _lastMouseX;
        float deltaY = position.Y - _lastMouseY;
        _lastMouseX = position.X;
        _lastMouseY = position.Y;

        if (_isRotating)
        {
            _camera.Rotate(-deltaX * RotationSensitivity, -deltaY * RotationSensitivity);
        }
        else if (_isPanning)
        {
            _camera.Pan(-deltaX * PanSensitivity, deltaY * PanSensitivity);
        }
    }

    private void OnScroll(IMouse mouse, ScrollWheel scroll)
    {
        _camera.Zoom(scroll.Y * ZoomSensitivity);
    }
}
