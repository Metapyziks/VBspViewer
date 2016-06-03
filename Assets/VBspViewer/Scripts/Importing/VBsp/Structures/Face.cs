using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Face
    {
        public ushort PlaneNum;
        public Side Side;

        [MarshalAs(UnmanagedType.U1)]
        public bool OnNode;

        public int FirstEdge;
        public short NumEdges;
        public short TexInfo;
        public short DispInfo;
        public short FogVolumeId;
        public int Styles;
        public int LightOffset;
        public float Area;
        public int LightMapOffsetX;
        public int LightMapOffsetY;
        public int LightMapSizeX;
        public int LightMapSizeY;
        public int OriginalFace;
        public ushort NumPrimitives;
        public ushort FirstPrimitive;
        public uint SmoothingGroups;
    }
}
