using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Leaf
    {
        public int Contents;
        public short Cluster;
        public short Flags;
        public short MinX, MinY, MinZ;
        public short MaxX, MaxY, MaxZ;
        public ushort FirstLeafFace;
        public ushort NumLeafFaces;
        public ushort FirstLeafBrush;
        public ushort NumLeafBrushes;
        public short LeafWaterDataId;

        private short _padding;
    }
}
