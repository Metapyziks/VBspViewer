using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Edge
    {
        public static Edge Read(BinaryReader reader)
        {
            return new Edge
            {
                A = reader.ReadUInt16(),
                B = reader.ReadUInt16()
            };
        }

        public ushort A;
        public ushort B;
    }
}
