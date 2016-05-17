using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Primitive
    {
        public static Primitive Read(BinaryReader reader)
        {
            return new Primitive
            {
                Type = (PrimitiveType) reader.ReadUInt16(),
                FirstIndex = reader.ReadUInt16(),
                IndexCount = reader.ReadUInt16(),
                FirstVert = reader.ReadUInt16(),
                VertCount = reader.ReadUInt16()
            };
        }

        public PrimitiveType Type;
        public ushort FirstIndex;
        public ushort IndexCount;
        public ushort FirstVert;
        public ushort VertCount;
    }
}
