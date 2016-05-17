using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DispSubNeighbor
    {
        public ushort NeighborIndex;
        public sbyte NeighborOrientation;
        public sbyte Span;
        public sbyte NeighborSpan;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DispNeighbor
    {
        public DispSubNeighbor SubNeighbor0;
        public DispSubNeighbor SubNeighbor1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DispCornerNeighbors
    {
        public ushort Neighbor0;
        public ushort Neighbor1;
        public ushort Neighbor2;
        public ushort Neighbor3;

        public byte NumNeighbors;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DispInfo
    {
        public Vector StartPosition;
        public int DispVertStart;
        public int DispTriStart;
        public int Power;
        public int MinTess;
        public float SmoothingAngle;
        public int Contents;
        public ushort MapFace;
        public int LightmapAlphaStart;
        public int LightmapSamplePositionStart;

        public DispNeighbor EdgeNeighbor0;
        public DispNeighbor EdgeNeighbor1;
        public DispNeighbor EdgeNeighbor2;
        public DispNeighbor EdgeNeighbor3;

        public DispCornerNeighbors CornerNeighbor0;
        public DispCornerNeighbors CornerNeighbor1;
        public DispCornerNeighbors CornerNeighbor2;
        public DispCornerNeighbors CornerNeighbor3;

        public uint AllowedVerts0;
        public uint AllowedVerts1;
        public uint AllowedVerts2;
        public uint AllowedVerts3;
        public uint AllowedVerts4;
        public uint AllowedVerts5;
        public uint AllowedVerts6;
        public uint AllowedVerts7;
        public uint AllowedVerts8;
        public uint AllowedVerts9;
    }
}
