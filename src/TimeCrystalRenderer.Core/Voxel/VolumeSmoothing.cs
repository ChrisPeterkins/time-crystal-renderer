namespace TimeCrystalRenderer.Core.Voxel;

/// <summary>
/// Applies a 3x3x3 box blur to a binary voxel volume, producing a smooth scalar field.
/// This transforms blocky voxel surfaces into organic, rounded geometry when fed to marching cubes.
/// </summary>
public static class VolumeSmoothing
{
    /// <summary>
    /// Converts a binary volume into a smoothed float volume using a 3x3x3 box blur.
    /// Each output voxel is the average of its 27 neighbors (including itself).
    /// Values range from 0.0 (no neighbors alive) to 1.0 (all neighbors alive).
    /// </summary>
    public static float[] BoxBlur3D(VoxelVolume volume)
    {
        int sizeX = volume.SizeX;
        int sizeY = volume.SizeY;
        int sizeZ = volume.SizeZ;
        var result = new float[sizeX * sizeY * sizeZ];

        for (int z = 0; z < sizeZ; z++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    int sum = 0;
                    int count = 0;

                    // Sample the 3x3x3 neighborhood
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                int nz = z + dz;

                                // Clamp to volume bounds
                                if (nx >= 0 && nx < sizeX &&
                                    ny >= 0 && ny < sizeY &&
                                    nz >= 0 && nz < sizeZ)
                                {
                                    if (volume[nx, ny, nz])
                                        sum++;
                                    count++;
                                }
                            }
                        }
                    }

                    result[x + y * sizeX + z * sizeX * sizeY] = (float)sum / count;
                }
            }
        }

        return result;
    }
}
