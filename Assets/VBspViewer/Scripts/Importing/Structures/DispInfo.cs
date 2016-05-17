using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DispSubNeighbor
    {
        public ushort NeighborIndex;
        public byte NeighborOrientation;
        public byte Span;
        public byte NeighborSpan;
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
        public short Unknown0;
        public int LightmapSamplePositionStart;

        public long Unknown1;
        public long Unknown2;
        public long Unknown3;
        public long Unknown4;
        public long Unknown5;
        public long Unknown6;
        public long Unknown7;
        public long Unknown8;
        public long Unknown9;
        public long Unknown10;
        public long Unknown11;
        public long Unknown12;
        public long Unknown13;
        public long Unknown14;
        public long Unknown15;
        public long Unknown16;
    }
}
