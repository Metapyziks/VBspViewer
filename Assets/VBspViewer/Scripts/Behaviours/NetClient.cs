using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Assets.VBspViewer.Scripts.Importing.Dem;
using JetBrains.Annotations;
using UnityEngine;
using VBspViewer.Behaviours.Entities;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem.Generated;

namespace VBspViewer.Behaviours
{
    public class NetClient : MonoBehaviour
    {
        [AttributeUsage(AttributeTargets.Method), MeansImplicitUse]
        private class GameEventHandlerAttribute : Attribute
        {
            public string EventName { get; set; }

            public GameEventHandlerAttribute(string eventName)
            {
                EventName = eventName;
            }
        }

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

        private delegate void GameEventHandler(NetClient self, CSVCMsgGameEvent message);

        private static Dictionary<string, MethodInfo> _sGameEventHandlerMethods;
        private static Dictionary<string, MethodInfo> GetGameEventHandlerMethods()
        {
            if (_sGameEventHandlerMethods != null) return _sGameEventHandlerMethods;

            _sGameEventHandlerMethods = new Dictionary<string, MethodInfo>();

            foreach (var method in typeof (NetClient).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attrib = (GameEventHandlerAttribute) method.GetCustomAttributes(typeof (GameEventHandlerAttribute), false).FirstOrDefault();
                if (attrib == null) continue;

                _sGameEventHandlerMethods.Add(attrib.EventName, method);
            }

            return _sGameEventHandlerMethods;
        } 

        private readonly Dictionary<int, CSVCMsgGameEventList.DescriptorT> _gameEvents = new Dictionary<int, CSVCMsgGameEventList.DescriptorT>();
        private readonly Dictionary<int, GameEventHandler> _gameEventHandlers = new Dictionary<int, GameEventHandler>();

        [PacketHandler]
        private void HandlePacket(CSVCMsgGameEventList message)
        {
            foreach (var descriptor in message.Descriptors)
            {
                if (_gameEvents.ContainsKey(descriptor.Eventid)) _gameEvents[descriptor.Eventid] = descriptor;
                else
                {
                    _gameEvents.Add(descriptor.Eventid, descriptor);
                }

                GenerateGameEventHandler(descriptor);
            }
        }

        private static Dictionary<GameEventType, MethodInfo> _sGameEventValueGetters;
        private static Dictionary<GameEventType, MethodInfo> GetGameEventValueGetters()
        {
            if (_sGameEventValueGetters != null) return _sGameEventValueGetters;

            _sGameEventValueGetters = new Dictionary<GameEventType, MethodInfo>();

            foreach (var type in Enum.GetValues(typeof(GameEventType)).Cast<GameEventType>())
            {
                var property = typeof (CSVCMsgGameEvent.KeyT).GetProperty("Val" + type);
                if (property == null) throw new Exception(string.Format("Could not find property for 'GameEventType.{0}'.", type));

                _sGameEventValueGetters.Add(type, property.GetGetMethod());
            }

            return _sGameEventValueGetters;
        }

        [UsedImplicitly]
        private static CSVCMsgGameEvent.KeyT GetGameEventValue(CSVCMsgGameEvent message, int index)
        {
            return message.Keys[index];
        }

        private void GenerateGameEventHandler(CSVCMsgGameEventList.DescriptorT descriptor)
        {
            MethodInfo method;
            if (!GetGameEventHandlerMethods().TryGetValue(descriptor.Name, out method)) return;

            var getters = GetGameEventValueGetters();

            var selfParam = Expression.Parameter(typeof (NetClient), "self");
            var messageParam = Expression.Parameter(typeof (CSVCMsgGameEvent), "message");

            var getValueMethod = typeof (NetClient).GetMethod("GetGameEventValue",
                BindingFlags.Static | BindingFlags.NonPublic);

            var parameters = method.GetParameters();
            var paramValues = new Expression[parameters.Length];

            for (var i = 0; i < parameters.Length; ++i)
            {
                var parameter = parameters[i];
                var keyIndex = descriptor.Keys.FindIndex(x => x.Name == parameter.Name);
                if (keyIndex == -1)
                {
                    throw new Exception(string.Format("Could not find a game event key for '{0}.{1}' in method '{2}'.",
                        descriptor.Name, parameter, method.Name));
                }

                var keyIndexConst = Expression.Constant(keyIndex);
                var getter = getters[(GameEventType) descriptor.Keys[keyIndex].Type];
                var getValueCall = Expression.Call(getValueMethod, messageParam, keyIndexConst);

                paramValues[i] = Expression.Call(getValueCall, getter);
            }

            var handlerCall = Expression.Call(selfParam, method, paramValues);
            var lambda = Expression.Lambda<GameEventHandler>(handlerCall, selfParam, messageParam);

            if (_gameEventHandlers.ContainsKey(descriptor.Eventid))
            {
                _gameEventHandlers[descriptor.Eventid] = lambda.Compile();
            }
            else
            {
                _gameEventHandlers.Add(descriptor.Eventid, lambda.Compile());
            }
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgServerInfo message)
        {
            ServerTickInterval = message.TickInterval;
        }

        public class PlayerInfo
        {
            public string Name { get; set; }
            public int Team { get; set; }
            public int EntityIndex { get; set; }
            public string NetworkId { get; set; }
            public string Address { get; set; }
        }

        private readonly Dictionary<int, PlayerInfo> _players = new Dictionary<int, PlayerInfo>();

        public PlayerInfo GetPlayerInfo(int userid)
        {
            PlayerInfo player;
            return _players.TryGetValue(userid, out player) ? player : null;
        }

        public PlayerInfo GetPlayerInfoFromEntityIndex(int entityindex)
        {
            return _players.FirstOrDefault(x => x.Value.EntityIndex == entityindex).Value;
        }

        [GameEventHandler("player_connect")]
        private void HandlePlayerConnect(string name, int index, int userid, string networkid, string address)
        {
            PlayerInfo player;
            if (!_players.TryGetValue(userid, out player))
            {
                player = new PlayerInfo();
                _players.Add(userid, player);
            }

            player.Name = name;
            player.EntityIndex = index;
            player.NetworkId = networkid;
            player.Address = address;
        }

        [GameEventHandler("player_team")]
        private void HandlePlayerConnect(int userid, int team, int oldteam, bool disconnect, bool silent)
        {
            if (disconnect) return;

            PlayerInfo player;
            if (!_players.TryGetValue(userid, out player)) return;

            player.Team = team;
        }

        [GameEventHandler("player_disconnect")]
        private void HandlePlayerConnect(string name, int userid, string reason)
        {
            PlayerInfo player;
            if (!_players.TryGetValue(userid, out player)) return;

            _players.Remove(userid);
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgGameEvent message)
        {
            CSVCMsgGameEventList.DescriptorT descriptor;
            if (!_gameEvents.TryGetValue(message.Eventid, out descriptor)) return;

            GameEventHandler handler;
            if (_gameEventHandlers.TryGetValue(message.Eventid, out handler))
            {
                handler(this, message);
            }

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

        private readonly Dictionary<string, StringTable> _stringTables = new Dictionary<string, StringTable>();

        public StringTable GetStringTable(string name)
        {
            return _stringTables[name];
        }

        private void ReadStringTables(BitBuffer buffer)
        {
            var numTables = buffer.ReadByte();
            for (var i = 0; i < numTables; ++i)
            {
                var tableName = buffer.ReadString(256);

                StringTable stringTable;
                if (!_stringTables.TryGetValue(tableName, out stringTable))
                {
                    stringTable = new StringTable();
                    _stringTables.Add(tableName, stringTable);
                }

                stringTable.Read(buffer);
            }
        }

        public void HandleStringTables(byte[] bytes, int length)
        {
            var buffer = new BitBuffer(bytes, length);

            ReadStringTables(buffer);
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgCreateStringTable message)
        {
            Debug.Log("FUCK");
        }

        [PacketHandler]
        private void HandlePacket(CSVCMsgUpdateStringTable message)
        {
            Debug.Log("THAT");
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
