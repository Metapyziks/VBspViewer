using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using SilentOrbit.ProtocolBuffers;
using UnityEngine;
using VBspViewer.Behaviours.Entities;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem;
using VBspViewer.Importing.Dem.Generated;

namespace VBspViewer.Behaviours
{
    public class NetClient : MonoBehaviour
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

        private delegate void PacketHandler(NetClient self, Stream stream, int length);

        private static readonly PacketHandler[] _sPacketHandlers;

        private static int GetPacketIdFromMessageType(Type type)
        {
            const string cNetPrefix = "CNETMsg";
            const string cSvcPrefix = "CSVCMsg";

            if (type.Name.StartsWith(cNetPrefix))
            {
                return (int) Enum.Parse(typeof(NETMessages), "net_" + type.Name.Substring(cNetPrefix.Length));
            }

            if (type.Name.StartsWith(cSvcPrefix))
            {
                return (int) Enum.Parse(typeof(SVCMessages), "svc_" + type.Name.Substring(cSvcPrefix.Length));
            }

            return -1;
        }

        static NetClient()
        {
            var handlers = new List<PacketHandler>();
            var paramSelf = Expression.Parameter(typeof (NetClient), "self");
            var paramStream = Expression.Parameter(typeof (Stream), "stream");
            var paramLength = Expression.Parameter(typeof (int), "length");

            foreach (var method in typeof(NetClient).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
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

        public float ServerTickInterval;
        public EntityManager EntityManager;

        public void HandlePacket(int msg, Stream stream, int length)
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
            ServerTickInterval = message.TickInterval;
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

        [PacketHandler]
        private void HandlePacket(CSVCMsgPacketEntities message)
        {
            EntityManager.HandlePacket(message);
        }
    }
}
