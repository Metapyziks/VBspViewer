using System;
using UnityEngine;

namespace VBspViewer.Importing.Dem
{
    public class BitBuffer
    {
        private static readonly uint[] _sMaskTable = {
            0,
            ( 1 << 1 ) - 1,
            ( 1 << 2 ) - 1,
            ( 1 << 3 ) - 1,
            ( 1 << 4 ) - 1,
            ( 1 << 5 ) - 1,
            ( 1 << 6 ) - 1,
            ( 1 << 7 ) - 1,
            ( 1 << 8 ) - 1,
            ( 1 << 9 ) - 1,
            ( 1 << 10 ) - 1,
            ( 1 << 11 ) - 1,
            ( 1 << 12 ) - 1,
            ( 1 << 13 ) - 1,
            ( 1 << 14 ) - 1,
            ( 1 << 15 ) - 1,
            ( 1 << 16 ) - 1,
            ( 1 << 17 ) - 1,
            ( 1 << 18 ) - 1,
            ( 1 << 19 ) - 1,
            ( 1 << 20 ) - 1,
            ( 1 << 21 ) - 1,
            ( 1 << 22 ) - 1,
            ( 1 << 23 ) - 1,
            ( 1 << 24 ) - 1,
            ( 1 << 25 ) - 1,
            ( 1 << 26 ) - 1,
            ( 1 << 27 ) - 1,
            ( 1 << 28 ) - 1,
            ( 1 << 29 ) - 1,
            ( 1 << 30 ) - 1,
            0x7fffffff,
            0xffffffff,
        };

        private readonly byte[] _buffer;
        private int _readOffset;
        private int _bitsAvailable;
        private uint _bufferDWord;
        private bool _overflow;

        public BitBuffer(byte[] buffer)
        {
            _buffer = buffer;
            _readOffset = 0;
            _bitsAvailable = 0;
        }

        private void FetchNext()
        {
            _bitsAvailable = 32;
            GrabNextDWord();
        }

        private void SetOverflowFlag()
        {
            _overflow = true;
        }

        private void GrabNextDWord(bool overflowImmediately = false)
        {
            if (_readOffset == _buffer.Length)
            {
                _bitsAvailable = 1;
                _bufferDWord = 0;
                _readOffset += sizeof(uint);

                if (overflowImmediately) SetOverflowFlag();

                return;
            }

            if (_readOffset > _buffer.Length)
            {
                SetOverflowFlag();
                _bufferDWord = 0;

                return;
            }

            Debug.Assert(_readOffset + sizeof(uint) <= _buffer.Length);
            _bufferDWord = BitConverter.ToUInt32(_buffer, _readOffset);
            _readOffset += sizeof (uint);
        }

        public uint ReadUBitLong(int bits)
        {
            if (_bitsAvailable >= bits)
            {
                var ret = _bufferDWord & _sMaskTable[bits];
                _bitsAvailable -= bits;

                if (_bitsAvailable > 0) _bufferDWord >>= bits;
                else FetchNext();

                return ret;
            }
            else
            {
                var ret = _bufferDWord;
                bits -= _bitsAvailable;

                GrabNextDWord(true);
                if (_overflow) return 0;

                ret |= ((_bufferDWord & _sMaskTable[bits]) << _bitsAvailable);
                _bitsAvailable = 32 - bits;
                _bufferDWord >>= bits;

                return ret;
            }
        }

        public uint ReadUBitVar()
        {
            var ret = ReadUBitLong(6);

            switch (ret & (16 | 32))
            {
                case 16:
                    ret = (ret & 15) | (ReadUBitLong(4) << 4);
                    Debug.Assert(ret >= 16);
                    break;
                case 32:
                    ret = (ret & 15) | (ReadUBitLong(8) << 4);
                    Debug.Assert(ret >= 256);
                    break;
                case 48:
                    ret = (ret & 15) | (ReadUBitLong(32 - 4) << 4);
                    Debug.Assert(ret >= 4096);
                    break;
            }

            return ret;
        }

        public bool ReadOneBit()
        {
            var ret = (_bufferDWord & 1) == 1;
            if (--_bitsAvailable == 0) FetchNext();
            else _bufferDWord >>= 1;

            return ret;
        }
    }
}
