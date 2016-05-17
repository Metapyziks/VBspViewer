using System.IO;

namespace VBspViewer.Importing.Structures
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

            var lumps = new LumpInfo[LumpInfoCount];
            for (var i = 0; i < LumpInfoCount; ++i)
            {
                lumps[i] = LumpInfo.Read(reader);
            }

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
