using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LumpInfo
    {
        public static LumpInfo Read(BinaryReader reader)
        {
            return new LumpInfo
            {
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32(),
                Version = reader.ReadInt32(),
                IdentCode = reader.ReadInt32()
            };
        }

        public int Offset;
        public int Length;
        public int Version;
        public int IdentCode;
    }
}
