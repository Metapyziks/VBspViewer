using System;
using System.IO;
using SilentOrbit.ProtocolBuffers;

namespace VBspViewer.Importing.Dem
{
    public class BitBuffer
    {
        private readonly byte[] _buffer;
        private int _readOffset;
        private int _length;

        public bool EndOfBuffer { get { return _readOffset >= _length; } }

        public byte[] InternalBuffer { get { return _buffer; } }

        public BitBuffer(byte[] buffer)
        {
            _buffer = buffer;
            _readOffset = 0;
            _length = buffer.Length;
        }

        public void SetLength(int length)
        {
            _length = length;
        }

        public void Seek(int offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _readOffset = offset;
                    break;
                case SeekOrigin.Current:
                    _readOffset += offset;
                    break;
                case SeekOrigin.End:
                    throw new NotImplementedException();
            }
        }

        public uint ReadVarInt32()
        {
            int b;
            uint val = 0;

            for (var n = 0; n < 5; n++)
            {
                if (_readOffset >= _length)
                {
                    throw new IOException("Buffer ended too early");
                }

                b = _buffer[_readOffset++];

                //Check that it fits in 32 bits
                if ((n == 4) && (b & 0xFE) != 0)
                {
                    throw new ProtocolBufferException("Got larger VarInt than 32 bit unsigned");
                }

                //End of check
                if ((b & 0x80) == 0)
                    return val | (uint) b << (7 * n);

                val |= (uint) (b & 0x7F) << (7 * n);
            }

            throw new ProtocolBufferException("Got larger VarInt than 32 bit unsigned");
        }
    }
}
