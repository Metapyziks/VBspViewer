using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using SilentOrbit.ProtocolBuffers;
using UnityEngine;
using VBspViewer.Behaviours;
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

        public NetClient NetClient { get; private set; }

        public string MapName { get; private set; }
        public bool DemoFinished { get; private set; }

        public bool ReadSignOn { get { return _stream.Position >= _signOnEndPos; } }

        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly int _startPos;
        private readonly int _signOnEndPos;

        private static string ReadString(BinaryReader reader, int length = 260)
        {
            var chars = reader.ReadChars(length);
            var end = Array.IndexOf(chars, '\0');
            return new string(chars, 0, end);
        }

        public DemFile(Stream stream, NetClient client)
        {
            _stream = stream;
            _reader = new BinaryReader(stream);

            NetClient = client;

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

            _startPos = (int) _stream.Position + sizeof (int);
            _signOnEndPos = _startPos + _reader.ReadInt32();
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
        [ThreadStatic]
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

            var length = _reader.ReadInt32();

            _sDemoPacketStream.Seek(0, SeekOrigin.Begin);
            _sDemoPacketStream.SetLength(length);

            _reader.BaseStream.Read(_sDemoPacketBuffer, 0, length);

            while (_sDemoPacketStream.Position < _sDemoPacketStream.Length)
            {
                var cmd = ProtocolParser.ReadUInt32(_sDemoPacketStream);
                var size = ProtocolParser.ReadUInt32(_sDemoPacketStream);
                var end = _sDemoPacketStream.Position + size;

                NetClient.HandlePacket((int) cmd, _sDemoPacketStream, (int) size);
                _sDemoPacketStream.Seek(end, SeekOrigin.Begin);
            }
        }

        [ThreadStatic]
        private static byte[] _sDemoRecordBuffer;
        [ThreadStatic]
        private static MemoryStream _sDemoRecordStream;

        private void HandleDemoRecord(Action<Stream> handler)
        {
            if (_sDemoRecordBuffer == null)
            {
                _sDemoRecordBuffer = new byte[2 * 1024 * 1024];
                _sDemoRecordStream = new MemoryStream(_sDemoRecordBuffer);
            }

            var length = _reader.ReadInt32();

            _sDemoRecordStream.Seek(0, SeekOrigin.Begin);
            _sDemoRecordStream.SetLength(length);

            _reader.BaseStream.Read(_sDemoRecordBuffer, 0, length);

            handler(_sDemoRecordStream);
        }

        private int _clientTick = 0;
        private float _deltaTime = 0f;

        public void Initialize()
        {
            _stream.Seek(_startPos, SeekOrigin.Begin);

            while (!ReadSignOn) ReadCommand();

            _deltaTime = 0f;
            _clientTick = 0;
        }

        public void Update(float dt)
        {
            _deltaTime += dt;

            var max = 16;
            while (_deltaTime > NetClient.ServerTickInterval && --max >= 0)
            {
                _deltaTime -= NetClient.ServerTickInterval;
                _clientTick += 1;

                while (ReadCommand(_clientTick)) ;
            }
        }

        public bool ReadCommand(int maxTick = int.MaxValue)
        {
            if (DemoFinished) return false;

            var curPos = _stream.Position;

            DemoCommand cmd;
            int tick, playerSlot;
            ReadCommandHeader(out cmd, out tick, out playerSlot);

            if (tick >= maxTick)
            {
                _stream.Seek(curPos, SeekOrigin.Begin);
                return false;
            }

            switch (cmd)
            {
                case DemoCommand.SyncTick:
                    return true;
                case DemoCommand.Stop:
                    DemoFinished = true;
                    return true;
                case DemoCommand.DataTables:
                    HandleDemoRecord(NetClient.EntityManager.HandleDataTables);
                    return true;
                case DemoCommand.ConsoleCmd:
                case DemoCommand.UserCmd:
                case DemoCommand.StringTables:
                case DemoCommand.CustomData:
                    ReadRawData(null);
                    return true;
                case DemoCommand.SignOn:
                case DemoCommand.Packet:
                    HandleDemoPacket();
                    return true;
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