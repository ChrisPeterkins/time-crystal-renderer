namespace TimeCrystalRenderer.Core.Voxel;

/// <summary>
/// Bit-packed 3D volume storing the spacetime history of a cellular automaton.
/// X and Y are the grid axes; Z is the time axis (generation number).
/// </summary>
public sealed class VoxelVolume
{
    private readonly ulong[] _data;

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public long TotalVoxels => (long)SizeX * SizeY * SizeZ;

    public VoxelVolume(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
        if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
        if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;

        long totalBits = TotalVoxels;
        int arrayLength = (int)((totalBits + 63) / 64);
        _data = new ulong[arrayLength];
    }

    // Each ulong stores 64 voxels. Divide by 64 (>> 6) to find the array slot,
    // mod 64 (& 63) to find the bit position within that slot.
    public bool this[int x, int y, int z]
    {
        get
        {
            int index = FlatIndex(x, y, z);
            return (_data[index >> 6] & (1UL << (index & 63))) != 0;
        }
        set
        {
            int index = FlatIndex(x, y, z);
            if (value)
                _data[index >> 6] |= 1UL << (index & 63);
            else
                _data[index >> 6] &= ~(1UL << (index & 63));
        }
    }

    /// <summary>
    /// Returns 1.0 if the voxel is solid, 0.0 if empty.
    /// Coordinates outside the volume return 0.0.
    /// </summary>
    public float Sample(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ)
            return 0f;

        return this[x, y, z] ? 1f : 0f;
    }

    private int FlatIndex(int x, int y, int z)
    {
        return x + y * SizeX + z * SizeX * SizeY;
    }
}
