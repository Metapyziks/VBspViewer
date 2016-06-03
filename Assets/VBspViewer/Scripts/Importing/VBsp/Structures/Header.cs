using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    public class Header
    {
        public const int LumpInfoCount = 64;

        public static Header Read(BinaryReader reader)
        {
            var header = new Header
            {
                Identifier = reader.ReadInt32(),
                Version = reader.ReadInt32()
            };

            var lumpInfoBytes = reader.ReadBytes(LumpInfoCount*Marshal.SizeOf(typeof (LumpInfo)));
            var lumps = ReadLumpWrapper<LumpInfo>.ReadLump(lumpInfoBytes, lumpInfoBytes.Length);

            header.Lumps = lumps;
            header.MapRevision = reader.ReadInt32();

            return header;
        }

        public int Identifier;
        public int Version;
        public LumpInfo[] Lumps;
        public int MapRevision;
    }
}
