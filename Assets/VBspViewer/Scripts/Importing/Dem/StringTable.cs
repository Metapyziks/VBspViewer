using System.Collections.Generic;
using System.Diagnostics;
using VBspViewer.Importing;

namespace Assets.VBspViewer.Scripts.Importing.Dem
{
    public class StringTable
    {
        private readonly List<string> _keys = new List<string>(); 
        private readonly Dictionary<string, byte[]> _strings = new Dictionary<string, byte[]>();

        private void ReadStrings(BitBuffer buffer)
        {
            var numStrings = buffer.ReadWord();
            while (_keys.Count < numStrings) _keys.Add(null);

            for (var i = 0; i < numStrings; ++i)
            {
                var name = buffer.ReadString(4096);
                _keys[i] = name;

                if (buffer.ReadOneBit())
                {
                    var userDataSize = (int) buffer.ReadWord();
                    Debug.Assert(userDataSize > 0);
                    var data = new byte[userDataSize];
                    buffer.ReadBytes(data, userDataSize);

                    if (!_strings.ContainsKey(name)) _strings.Add(name, data);
                    else _strings[name] = data;
                }
            }
        }

        public void Read(BitBuffer buffer)
        {
            ReadStrings(buffer);
            if (!buffer.ReadOneBit()) return;
            ReadStrings(buffer);
        }

        public int Count { get { return _keys.Count; } }

        public string this[int index]
        {
            get { return _keys[index]; }
        }

        public bool TryGetUserData(string name, out byte[] value)
        {
            return _strings.TryGetValue(name, out value);
        }

        public byte[] this[string name]
        {
            get { return _strings[name]; }
        }
    }
}
