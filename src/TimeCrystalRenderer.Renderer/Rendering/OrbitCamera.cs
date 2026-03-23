using System.Numerics;

namespace TimeCrystalRenderer.Renderer.Rendering;

/// <summary>
/// An orbit camera that rotates around a target point using spherical coordinates.
/// </summary>
public sealed class OrbitCamera
{
    private const float MinPitch = -89f * MathF.PI / 180f;
    private const float MaxPitch = 89f * MathF.PI / 180f;
    private const float MinDistance = 1f;

    /// <summary>
    /// The point the camera orbits around.
    /// </summary>
    public Vector3 Target { get; set; }

    /// <summary>
    /// Distance from the target point.
    /// </summary>
    public float Distance { get; set; } = 100f;

    /// <summary>
    /// Horizontal angle in radians.
    /// </summary>
    public float Yaw { get; set; } = MathF.PI / 4f;

    /// <summary>
    /// Vertical angle in radians, clamped to avoid gimbal lock.
    /// </summary>
    public float Pitch { get; set; } = MathF.PI / 6f;

    /// <summary>
    /// Field of view in degrees.
    /// </summary>
    public float FieldOfView { get; set; } = 45f;

    public Vector3 Position
    {
        get
        {
            float cosPitch = MathF.Cos(Pitch);
            return Target + new Vector3(
                Distance * cosPitch * MathF.Sin(Yaw),
                Distance * MathF.Sin(Pitch),
                Distance * cosPitch * MathF.Cos(Yaw));
        }
    }

    public Matrix4x4 GetViewMatrix()
    {
        return Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);
    }

    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        float fovRadians = FieldOfView * MathF.PI / 180f;
        return Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, 0.1f, 10000f);
    }

    /// <summary>
    /// Rotates the camera around the target.
    /// </summary>
    public void Rotate(float deltaYaw, float deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch = Math.Clamp(Pitch + deltaPitch, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Moves the camera closer to or farther from the target.
    /// </summary>
    public void Zoom(float delta)
    {
        Distance = MathF.Max(MinDistance, Distance - delta);
    }

    /// <summary>
    /// Pans the target point relative to the camera's current orientation.
    /// </summary>
    public void Pan(float deltaX, float deltaY)
    {
        // Build a right/up basis from the camera's current orientation
        Vector3 forward = Vector3.Normalize(Target - Position);
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        Vector3 up = Vector3.Cross(right, forward);

        Target += right * deltaX + up * deltaY;
    }
}
