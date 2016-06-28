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
using VBspViewer.Importing.Dem.Generated;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.Dem
{
    public class DemFile : IDisposable
    {
        [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
        private class PacketHandlerAttribute : Attribute
        {
            public int PacketId { get; set; }

            public PacketHandlerAttribute()
            {
                PacketId = -1;
            }

            public PacketHandlerAttribute(NETMessages packetId)
            {
                PacketId = (int) packetId;
            }

            public PacketHandlerAttribute(SVCMessages packetId)
            {
                PacketId = (int) packetId;
            }
        }

        private delegate void PacketHandler(DemFile self, Stream stream, int length);

        private static readonly PacketHandler[] _sPacketHandlers;

        private static int GetPacketIdFromMessageType(Type type)
        {
            const string cNetPrefix = "CNETMsg";
            const string cSvcPrefix = "CSVCMsg";

            if (type.Name.StartsWith(cNetPrefix))
            {
                return (int) Enum.Parse(typeof (NETMessages), "net_" + type.Name.Substring(cNetPrefix.Length));
            }

            if (type.Name.StartsWith(cSvcPrefix))
            {
                return (int) Enum.Parse(typeof (SVCMessages), "svc_" + type.Name.Substring(cSvcPrefix.Length));
            }

            return -1;
        }

        static DemFile()
        {
            var handlers = new List<PacketHandler>();
            var paramSelf = Expression.Parameter(typeof (DemFile), "self");
            var paramStream = Expression.Parameter(typeof (Stream), "stream");
            var paramLength = Expression.Parameter(typeof (int), "length");

            foreach (var method in typeof(DemFile).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attrib = method.GetCustomAttributes(typeof (PacketHandlerAttribute), false).Cast<PacketHandlerAttribute>().FirstOrDefault();
                if (attrib == null) continue;

                var parms = method.GetParameters();
                if (parms.Length != 1) continue;

                var packetId = attrib.PacketId != -1 ? attrib.PacketId : GetPacketIdFromMessageType(parms[0].ParameterType);
                if (packetId < 0) continue;

                var deserialize = parms[0].ParameterType.GetMethod("DeserializeLength",
                    BindingFlags.Public | BindingFlags.Static,
                    null, new [] { typeof (Stream), typeof(int) }, null);
                
                var callDeserialize = Expression.Call(deserialize, paramStream, paramLength);
                var castMessage = Expression.Convert(callDeserialize, parms[0].ParameterType);
                var callHandle = Expression.Call(paramSelf, method, castMessage);
                var lambda = Expression.Lambda<PacketHandler>(callHandle, paramSelf, paramStream, paramLength);

                while (handlers.Count <= packetId) handlers.Add(null);

                handlers[packetId] = lambda.Compile();
            }

            _sPacketHandlers = handlers.ToArray();
        }

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
        public float TickInterval { get; private set; }

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

        [ThreadStatic]
        private static StringBuilder _sBuilder;
        private static string ReadVarLengthString(BinaryReader reader, int maxLength = 256)
        {
            if (_sBuilder == null) _sBuilder = new StringBuilder();
            else _sBuilder.Remove(0, _sBuilder.Length);

            char c;
            while ((c = reader.ReadChar()) != '\0')
            {
                _sBuilder.Append(c);
            }

            return _sBuilder.ToString();
        }

        public DemFile(Stream stream)
        {
            TickInterval = float.MaxValue;

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

            _sDemoPacketStream.Seek(0, SeekOrigin.Begin);
            _sDemoPacketStream.SetLength(ReadRawData(_sDemoPacketBuffer));

            while (_sDemoPacketStream.Position < _sDemoPacketStream.Length)
            {
                var cmd = ProtocolParser.ReadUInt32(_sDemoPacketStream);
                var size = ProtocolParser.ReadUInt32(_sDemoPacketStream);
                var end = _sDemoPacketStream.Position + size;

                HandlePacket((int) cmd, _sDemoPacketStream, (int) size);
                _sDemoPacketStream.Seek(end, SeekOrigin.Begin);
            }
        }

        private void HandlePacket(int msg, Stream stream, int length)
        {
            if (msg < 0 || msg >= _sPacketHandlers.Length) return;

            var handler = _sPacketHandlers[msg];
            if (handler == null) return;

            handler(this, stream, length);
        }

        private enum GameEventType
        {
            String = 1,
            Float = 2,
            Long = 3,
            Short = 4,
            Byte = 5,
            Bool = 6,
            Uint64 = 7,
            Wstring = 8
        }

        private readonly Dictionary<int, CSVCMsgGameEventList.DescriptorT> _gameEvents = new Dictionary<int, CSVCMsgGameEventList.DescriptorT>();

        [PacketHandler]
        private void HandlePacket(CSVCMsgGameEventList message)
        {
            foreach (var descriptor in message.Descriptors)
            {
                if (_gameEvents.ContainsKey(descriptor.Eventid)) _gameEvents[descriptor.Eventid] = descriptor;
                else _gameEvents.Add(descriptor.Eventid, descriptor);
            }
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgServerInfo message)
        {
            TickInterval = message.TickInterval;
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgGameEvent message)
        {
            CSVCMsgGameEventList.DescriptorT descriptor;
            if (!_gameEvents.TryGetValue(message.Eventid, out descriptor)) return;

            if (descriptor.Name == "player_footstep") return;

            var builder = new StringBuilder();
            builder.AppendFormat("{0}", descriptor.Name);
            builder.AppendLine();

            if (descriptor.Keys.Count > 0)
            {
                for (var i = 0; i < descriptor.Keys.Count; ++i)
                {
                    var keyDesc = descriptor.Keys[i];
                    var keyVal = message.Keys[i];

                    builder.AppendFormat("  {0}: ", keyDesc.Name);

                    switch ((GameEventType) keyVal.Type)
                    {
                        case GameEventType.String: builder.AppendFormat("\"{0}\"", keyVal.ValString); break;
                        case GameEventType.Float: builder.Append(keyVal.ValFloat); break;
                        case GameEventType.Long: builder.Append(keyVal.ValLong); break;
                        case GameEventType.Short: builder.Append(keyVal.ValShort); break;
                        case GameEventType.Byte: builder.Append(keyVal.ValByte); break;
                        case GameEventType.Bool: builder.Append(keyVal.ValBool); break;
                        case GameEventType.Uint64: builder.Append(keyVal.ValUint64); break;
                        case GameEventType.Wstring: builder.AppendFormat("\"{0}\"", Encoding.Unicode.GetString(keyVal.ValWstring)); break;
                        default: builder.Append("?"); break;
                    }

                    builder.AppendLine();
                }
            }

            Debug.Log(builder);
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgSendTable message)
        {
            Debug.Log(message.NetTableName);
        }

        private const int EntitySentinel = 9999;

        private enum UpdateType
        {
            EnterPVS = 0,
            LeavePVS,
            DeltaEnt,
            PreserveEnt,
            Finished,
            Failed
        }

        [Flags]
        private enum UpdateFlags
        {
            Zero = 0,
            LeavePvs = 1,
            Delete = 2,
            EnterPvs = 4
        }

        private class Entity
        {
            public int Id { get; private set; }
            public uint ClassId { get; private set; }
            public uint SerialNum { get; private set; }

            public Entity(int id, uint classId, uint serialNum)
            {
                Id = id;

                Init(classId, serialNum);
            }

            public void Init(uint classId, uint serialNum)
            {
                ClassId = classId;
                SerialNum = serialNum;
            }
        }

        private readonly List<Entity> _entities = new List<Entity>();

        private Entity FindEntity(int entId)
        {
            for (var i = _entities.Count - 1; i >= 0; --i)
            {
                if (_entities[i].Id == entId) return _entities[i];
            }

            return null;
        }

        private Entity AddEntity(int entId, uint classId, uint serialNum)
        {
            var ent = FindEntity(entId);
            if (ent != null) ent.Init(classId, serialNum);
            else _entities.Add(ent = new Entity(entId, classId, serialNum));

            return ent;
        }

        private void RemoveEntity(int entId)
        {
            for (var i = _entities.Count - 1; i >= 0; --i)
            {
                if (_entities[i].Id == entId)
                {
                    _entities.RemoveAt(i);
                    return;
                }
            }
        }

        private int ReadFieldIndex(BitBuffer bitBuffer, int oldIndex, bool newWay)
        {
            if (newWay && bitBuffer.ReadOneBit()) return oldIndex + 1;

            int ret;
            if (newWay && bitBuffer.ReadOneBit())
            {
                ret = (int) bitBuffer.ReadUBitLong(3);
            }
            else
            {
                ret = (int) bitBuffer.ReadUBitLong(7);
                switch (ret & (32 | 64))
                {
                    case 32:
                        ret = (ret & ~96) | (int) (bitBuffer.ReadUBitLong(2) << 5);
                        Debug.Assert(ret >= 32);
                        break;
                    case 64:
                        ret = (ret & ~96) | (int) (bitBuffer.ReadUBitLong(4) << 5);
                        Debug.Assert(ret >= 128);
                        break;
                    case 96:
                        ret = (ret & ~96) | (int) (bitBuffer.ReadUBitLong(7) << 5);
                        Debug.Assert(ret >= 512);
                        break;
                }
            }

            if (ret == 0xfff)
            {
                return -1;
            }

            return oldIndex + 1 + ret;
        }

        [ThreadStatic]
        private static List<int> _fieldIndices; 
        private void ReadNewEntity(BitBuffer bitBuffer, Entity ent)
        {
            if (_fieldIndices == null) _fieldIndices = new List<int>();
            else _fieldIndices.Clear();

            var newWay = bitBuffer.ReadOneBit();

            var index = -1;
            while (true)
            {
                index = ReadFieldIndex(bitBuffer, index, newWay);
                if (index == -1) break;
                _fieldIndices.Add(index);
            }

            var table = _serverClasses[(int) ent.ClassId].DataTable;

            for (var i = 0; i < _fieldIndices.Count; ++i)
            {
                index = _fieldIndices[i];
                
                throw new NotImplementedException();
            }
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgPacketEntities message)
        {
            var headerCount = message.UpdatedEntries;
            var updateType = UpdateType.PreserveEnt;
            var updateFlags = UpdateFlags.Zero;
            var headerBase = -1;
            var newEntity = -1;

            var bitBuffer = new BitBuffer(message.EntityData);

            while (updateType < UpdateType.Finished)
            {
                var isEntity = headerCount-- >= 0;
                if (isEntity)
                {
                    updateFlags = UpdateFlags.Zero;
                    newEntity = headerBase + 1 + (int) bitBuffer.ReadUBitVar();
                    headerBase = newEntity;

                    if (!bitBuffer.ReadOneBit())
                    {
                        if (bitBuffer.ReadOneBit()) updateFlags |= UpdateFlags.EnterPvs;
                    }
                    else
                    {
                        updateFlags |= UpdateFlags.LeavePvs;
                        if (bitBuffer.ReadOneBit()) updateFlags |= UpdateFlags.Delete;
                    }
                }
                
                for (updateType = UpdateType.PreserveEnt; updateType == UpdateType.PreserveEnt;)
                {
                    if (!isEntity || newEntity > EntitySentinel) updateType = UpdateType.Finished;
                    else if ((updateFlags & UpdateFlags.EnterPvs) == UpdateFlags.EnterPvs) updateType = UpdateType.EnterPVS;
                    else if ((updateFlags & UpdateFlags.LeavePvs) == UpdateFlags.EnterPvs) updateType = UpdateType.LeavePVS;
                    else updateType = UpdateType.DeltaEnt;

                    switch (updateType)
                    {
                        case UpdateType.EnterPVS:
                        {
                            var classId = bitBuffer.ReadUBitLong(_serverClassBits);
                            var serialNum = bitBuffer.ReadUBitLong(10);
                            var ent = AddEntity(newEntity, classId, serialNum);

                            ReadNewEntity(bitBuffer, ent);

                            break;
                        }

                        case UpdateType.LeavePVS:
                        {
                            Debug.Assert(message.IsDelta);
                            RemoveEntity(newEntity);
                            break;
                        }

                        case UpdateType.DeltaEnt:
                        {
                            var ent = FindEntity(newEntity);
                            Debug.Assert(ent != null);

                            ReadNewEntity(bitBuffer, ent);
                            break;
                        }

                        case UpdateType.PreserveEnt:
                        {
                            Debug.Assert(message.IsDelta);
                            Debug.Assert(newEntity < 1 << 11);
                            break;
                        }
                    }
                }
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

            _sDemoRecordStream.Seek(0, SeekOrigin.Begin);
            _sDemoRecordStream.SetLength(ReadRawData(_sDemoRecordBuffer));

            handler(_sDemoRecordStream);
        }

        private class ServerClass
        {
            public short ClassId { get; private set; }
            public string Name { get; private set; }
            public string DataTableName { get; private set; }
            public CSVCMsgSendTable DataTable { get; set; }

            public ServerClass(BinaryReader reader)
            {
                ClassId = reader.ReadInt16();
                Name = ReadVarLengthString(reader);
                DataTableName = ReadVarLengthString(reader);
            }

            public override string ToString()
            {
                return Name;
            }
        }

        private readonly List<CSVCMsgSendTable> _sendTables = new List<CSVCMsgSendTable>();
        private readonly List<ServerClass> _serverClasses = new List<ServerClass>();
        private int _serverClassBits;

        private void HandleDataTables(Stream stream)
        {
            while (true)
            {
                var type = ProtocolParser.ReadUInt32(stream);
                var size = ProtocolParser.ReadUInt32(stream);
                var table = CSVCMsgSendTable.DeserializeLength(stream, (int) size);

                if (table.IsEnd) break;

                _sendTables.Add(table);
            }

            var reader = new BinaryReader(stream);
            var svClassCount = reader.ReadInt16();
            
            for (var i = 0; i < svClassCount; ++i)
            {
                var svClass = new ServerClass(reader);
                svClass.DataTable = _sendTables.FirstOrDefault(x => x.NetTableName == svClass.DataTableName);

                _serverClasses.Add(svClass);
            }

            _serverClassBits = 0;
            var temp = svClassCount;
            while ((temp >>= 1) > 0) ++_serverClassBits;
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
            while (_deltaTime > TickInterval)
            {
                _deltaTime -= TickInterval;
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

            CurrentTick = tick;

            switch (cmd)
            {
                case DemoCommand.SyncTick:
                    return true;
                case DemoCommand.Stop:
                    DemoFinished = true;
                    return true;
                case DemoCommand.DataTables:
                    HandleDemoRecord(HandleDataTables);
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