using System;
using System.IO;
using System.Linq;
using Ionic.Zip;
using VBspViewer.Importing;

namespace Assets.VBspViewer.Scripts.Importing.VBsp
{
    public class PakFile : IResourceProvider
    {
        private readonly ZipFile _zipFile;

        public PakFile(byte[] contents)
        {
            _zipFile = ZipFile.Read(new MemoryStream(contents));

            File.WriteAllLines("pakfile.txt", _zipFile.Entries.Select(x => string.Format("{0}: {1:F2}KB", x.FileName, x.UncompressedSize / 1024f)).ToArray());
        }

        public bool ContainsFile(string filename)
        {
            return _zipFile.ContainsEntry(filename);
        }

        [ThreadStatic]
        private static byte[] _sReadBuffer;
        private const int ReadLength = 2048;

        public Stream OpenFile(string filename)
        {
            if (_sReadBuffer == null) _sReadBuffer = new byte[ReadLength];

            var entry = _zipFile[filename];
            var copyStream = new MemoryStream((int) entry.UncompressedSize);
            using (var fileStream = _zipFile[filename].OpenReader())
            {
                for (var read = 0; read < entry.UncompressedSize;)
                {
                    var added = fileStream.Read(_sReadBuffer, 0, ReadLength);
                    copyStream.Write(_sReadBuffer, 0, added);
                    read += added;
                }
            }

            copyStream.Seek(0, SeekOrigin.Begin);
            return copyStream;
        }
    }
}
