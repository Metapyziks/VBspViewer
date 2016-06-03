using System.IO;

namespace VBspViewer.Importing.VBsp.Structures
{
    public class GameLumpInfo
    {
        public int Id { get; private set; }
        public ushort Flags { get; private set; }
        public ushort Version { get; private set; }
        public int FileOffset { get; private set; }
        public int FileLength { get; private set; }

        public byte[] Contents { get; private set; }

        public GameLumpInfo(BinaryReader reader)
        {
            Id = reader.ReadInt32();
            Flags = reader.ReadUInt16();
            Version = reader.ReadUInt16();
            FileOffset = reader.ReadInt32();
            FileLength = reader.ReadInt32();
        }

        public void ReadContents(Stream stream)
        {
            Contents = new byte[FileLength];
            stream.Seek(FileOffset, SeekOrigin.Begin);
            stream.Read(Contents, 0, FileLength);
        }
    }
}
