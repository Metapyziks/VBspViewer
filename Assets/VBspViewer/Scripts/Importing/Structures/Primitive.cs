using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Primitive
    {
        public PrimitiveType Type;
        public ushort FirstIndex;
        public ushort IndexCount;
        public ushort FirstVert;
        public ushort VertCount;
    }
}
