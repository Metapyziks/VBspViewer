using System;
using System.IO;
using System.Runtime.InteropServices;
using SilentOrbit.ProtocolBuffers;
using UnityEngine;
using VBspViewer.Importing.Dem.Generated;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.Dem
{
    public class DemFile : IDisposable
    {
        private enum DemoCommand : byte
        {
            SignOn = 1,
            Packet,
            SyncTick,
            ConsoleCmd,
            UserCmd,
            DataTables,
            Stop,
            CustomData,
            StringTables,
            LastCmd = StringTables
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DemoCommandInfo
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Split
            {
                public int Flags;
                public Vector ViewOrigin;
                public Vector ViewAngles;
                public Vector LocalViewAngles;

                public Vector ViewOrigin2;
                public Vector ViewAngles2;
                public Vector LocalViewAngles2;
            }

            public Split A;
            public Split B;
        }

        public string MapName { get; private set; }
        public int CurrentTick { get; private set; }
        public bool DemoFinished { get; private set; }

        public bool ReadSignOn { get { return _stream.Position >= _startPos; } }

        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly int _startPos;

        private static string ReadString(BinaryReader reader, int length = 260)
        {
            var chars = reader.ReadChars(length);
            var end = Array.IndexOf(chars, '\0');
            return new string(chars, 0, end);
        }

        public DemFile(Stream stream)
        {
            _stream = stream;
            _reader = new BinaryReader(stream);
            
            Debug.Assert(new string(_reader.ReadChars(8)) == "HL2DEMO\0");

            Debug.Assert(_reader.ReadInt32() == 4);
            Debug.Assert(_reader.ReadInt32() == 0x34e2);

            var serverName = ReadString(_reader);
            var clientName = ReadString(_reader);
            MapName = ReadString(_reader);
            var gameDirectory = ReadString(_reader);

            var playbackTime = _reader.ReadSingle();
            var playbackTicks = _reader.ReadInt32();
            var playbackFrames = _reader.ReadInt32();

            _startPos = _reader.ReadInt32();
            _startPos += (int) _reader.BaseStream.Position;
        }

        private void ReadCommandHeader(out DemoCommand cmd, out int tick, out int playerSlot)
        {
            cmd = (DemoCommand) _reader.ReadByte();

            Debug.Assert((int) cmd >= 1 && cmd <= DemoCommand.LastCmd);

            tick = _reader.ReadInt32();
            playerSlot = _reader.ReadByte();
        }

        private int ReadRawData(byte[] buffer)
        {
            var length = _reader.ReadInt32();

            if (buffer == null)
            {
                _reader.BaseStream.Seek(length, SeekOrigin.Current);
                return length;
            }

            if (buffer.Length < length)
            {
                throw new ArgumentException(string.Format("Raw data buffer is too small ({0} < {1}).", buffer.Length, length));
            }

            return _reader.BaseStream.Read(buffer, 0, length);
        }

        [ThreadStatic]
        private static byte[] _sDemoPacketBuffer;
        private static MemoryStream _sDemoPacketStream;

        private void HandleDemoPacket()
        {
            var info = ReadLumpWrapper<DemoCommandInfo>.ReadSingleFromStream(_stream);
            var seqNrIn = _reader.ReadInt32();
            var seqNrOut = _reader.ReadInt32();

            if (_sDemoPacketStream == null)
            {
                _sDemoPacketBuffer = new byte[262140];
                _sDemoPacketStream = new MemoryStream(_sDemoPacketBuffer);
            }

            _sDemoPacketStream.Seek(0, SeekOrigin.Begin);
            _sDemoPacketStream.SetLength(ReadRawData(_sDemoPacketBuffer));

            while (_sDemoPacketStream.Position < _sDemoPacketStream.Length)
            {
                var cmd = ProtocolParser.ReadUInt32(_sDemoPacketStream);
                var size = ProtocolParser.ReadUInt32(_sDemoPacketStream);

                if (cmd == 0) { /* nop */ }
                else if (cmd < 8)
                {
                    NETMessages msg = (NETMessages) cmd;
                    Debug.LogFormat("Packet: {0}", msg);
                }
                else if (cmd < 32)
                {
                    SVCMessages msg = (SVCMessages) cmd;
                    Debug.LogFormat("Packet: {0}", msg);
                }
                else
                {
                    Debug.LogFormat("Unrecognised packet: {0}", cmd);
                }

                _sDemoPacketStream.Seek(size, SeekOrigin.Current);
            }
        }

        public void ReadCommand()
        {
            DemoCommand cmd;
            int tick, playerSlot;
            ReadCommandHeader(out cmd, out tick, out playerSlot);

            Debug.LogFormat("Tick: {0}, DemoCommand: {1}", tick, cmd);

            CurrentTick = tick;

            switch (cmd)
            {
                case DemoCommand.SyncTick:
                    break;
                case DemoCommand.Stop:
                    DemoFinished = true;
                    break;
                case DemoCommand.ConsoleCmd:
                case DemoCommand.DataTables:
                case DemoCommand.UserCmd:
                case DemoCommand.StringTables:
                case DemoCommand.CustomData:
                    ReadRawData(null);
                    break;
                case DemoCommand.SignOn:
                case DemoCommand.Packet:
                    HandleDemoPacket();
                    break;
                default:
                    throw new Exception("Unrecognised demo command.");
            }
        }

        public void Dispose()
        {
            _reader.Close();
            _stream.Dispose();
        }
    }
}