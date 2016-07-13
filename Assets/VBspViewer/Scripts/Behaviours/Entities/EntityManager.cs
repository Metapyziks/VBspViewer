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
using VBspViewer.Importing;
using VBspViewer.Importing.Dem.Generated;
using VBspViewer.Importing.Entities;

namespace VBspViewer.Behaviours.Entities
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ClassNameAttribute : Attribute
    {
        public string HammerName { get; set; }
        public string ClassName { get; set; }
    }

    public struct FlattenedProperty
    {
        public readonly CSVCMsgSendTable.SendpropT Property;
        public readonly CSVCMsgSendTable.SendpropT ArrayElementProperty;

        public FlattenedProperty(CSVCMsgSendTable.SendpropT prop, CSVCMsgSendTable.SendpropT arrayElemProp)
        {
            Property = prop;
            ArrayElementProperty = arrayElemProp;
        }

        public override string ToString()
        {
            return Property.VarName;
        }
    }

    public class EntityManager : MonoBehaviour
    {
        private delegate BaseEntity HammerNameCtor(EntityManager self);
        private delegate BaseEntity ClassNameCtor(EntityManager self, int entId, int classId, uint serialNum);

        private static readonly Dictionary<string, HammerNameCtor> _sHammerNameCtors = new Dictionary<string, HammerNameCtor>();
        private static readonly Dictionary<string, ClassNameCtor> _sClassNameCtors = new Dictionary<string, ClassNameCtor>();

        static EntityManager()
        {
            var createEntity = typeof (EntityManager)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name == "CreateEntity" && x.IsGenericMethod);

            var addEntity = typeof (EntityManager)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name == "AddEntity" && x.IsGenericMethod);

            var selfParam = Expression.Parameter(typeof (EntityManager), "self");
            var endIdParam = Expression.Parameter(typeof (int), "entId");
            var classIdParam = Expression.Parameter(typeof (int), "classId");
            var serialNumParam = Expression.Parameter(typeof (uint), "serialNum");

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var attrib = (ClassNameAttribute) type.GetCustomAttributes(typeof (ClassNameAttribute), false).FirstOrDefault();
                if (attrib == null) continue;

                if (attrib.HammerName != null)
                {
                    var method = createEntity.MakeGenericMethod(type);
                    var call = Expression.Call(selfParam, method);
                    var lambda = Expression.Lambda<HammerNameCtor>(call, selfParam);

                    _sHammerNameCtors.Add(attrib.HammerName, lambda.Compile());
                }

                if (attrib.ClassName != null)
                {
                    var method = addEntity.MakeGenericMethod(type);
                    var call = Expression.Call(selfParam, method, endIdParam, classIdParam, serialNumParam);
                    var lambda = Expression.Lambda<ClassNameCtor>(call, selfParam, endIdParam, classIdParam, serialNumParam);

                    _sClassNameCtors.Add(attrib.ClassName, lambda.Compile());
                }
            }
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

        private readonly List<ServerClass> _serverClasses = new List<ServerClass>();

        private class ServerClass
        {
            public short ClassId { get; private set; }
            public string Name { get; private set; }
            public string DataTableName { get; private set; }
            public CSVCMsgSendTable DataTable { get; set; }
            public List<FlattenedProperty> FlattenedProps { get; private set; }

            public ServerClass(BinaryReader reader)
            {
                ClassId = reader.ReadInt16();
                Name = ReadVarLengthString(reader);
                DataTableName = ReadVarLengthString(reader);
                FlattenedProps = new List<FlattenedProperty>();
            }

            public override string ToString()
            {
                return Name;
            }

            [ThreadStatic]
            private static List<int> _sPriorities; 

            public void SortFlattenedProps()
            {
                if (_sPriorities == null) _sPriorities = new List<int>();
                else _sPriorities.Clear();

                _sPriorities.Add(64);

                foreach (var flattenedProperty in FlattenedProps)
                {
                    var priority = flattenedProperty.Property.Priority;
                    var inserted = false;

                    for (var i = 0; i < _sPriorities.Count; ++i)
                    {
                        if (_sPriorities[i] < priority) continue;

                        inserted = true;

                        if (_sPriorities[i] > priority)
                        {
                            _sPriorities.Insert(i, priority);
                        }

                        break;
                    }

                    if (!inserted) _sPriorities.Add(priority);
                }

                var start = 0;
                for (var i = 0; i < _sPriorities.Count; ++i)
                {
                    var priority = _sPriorities[i];

                    while (true)
                    {
                        var current = start;
                        while (current < FlattenedProps.Count)
                        {
                            var prop = FlattenedProps[current].Property;

                            if (prop.Priority == priority ||
                                (priority == 64 && ((SendPropFlag) prop.Flags & SendPropFlag.CHANGES_OFTEN) != 0))
                            {
                                if (start != current)
                                {
                                    var temp = FlattenedProps[start];
                                    FlattenedProps[start] = FlattenedProps[current];
                                    FlattenedProps[current] = temp;
                                }

                                ++start;
                                break;
                            }
                            ++current;
                        }

                        if (current == FlattenedProps.Count) break;
                    }
                }
            }
        }

        private struct ExcludeEntry
        {
            public readonly string VarName;
            public readonly string DtName;
            public readonly string DtExcluding;

            public ExcludeEntry(string varName, string dtName, string dtExcluding)
            {
                VarName = varName;
                DtName = dtName;
                DtExcluding = dtExcluding;
            }
        }

        private readonly List<CSVCMsgSendTable> _sendTables = new List<CSVCMsgSendTable>();
        private int _serverClassBits;

        private readonly List<ExcludeEntry> _excludes = new List<ExcludeEntry>();

        private CSVCMsgSendTable GetTableByName(string name)
        {
            for (var i = 0; i < _sendTables.Count; ++i)
            {
                if (_sendTables[i].NetTableName == name) return _sendTables[i];
            }

            return null;
        }

        private void GatherExcludes(CSVCMsgSendTable table)
        {
            for (var i = 0; i < table.Props.Count; ++i)
            {
                var prop = table.Props[i];
                if (((SendPropFlag) prop.Flags & SendPropFlag.EXCLUDE) != 0)
                {
                    _excludes.Add(new ExcludeEntry(prop.VarName, prop.DtName, table.NetTableName));
                }

                if ((SendPropType) prop.Type == SendPropType.DataTable)
                {
                    var subTable = GetTableByName(prop.DtName);
                    if (subTable != null) GatherExcludes(subTable);
                }
            }
        }

        private bool IsPropExcluded(CSVCMsgSendTable table, CSVCMsgSendTable.SendpropT prop)
        {
            for (var i = 0; i < _excludes.Count; ++i)
            {
                var exclude = _excludes[i];
                if (table.NetTableName == exclude.DtName && prop.VarName == exclude.VarName) return true;
            }

            return false;
        }

        private void GatherProps_IterateProps(CSVCMsgSendTable table, int classId, List<FlattenedProperty> props)
        {
            for (var i = 0; i < table.Props.Count; ++i)
            {
                var prop = table.Props[i];

                if (((SendPropFlag) prop.Flags & SendPropFlag.INSIDEARRAY) != 0 ||
                    ((SendPropFlag) prop.Flags & SendPropFlag.EXCLUDE) != 0 || IsPropExcluded(table, prop))
                {
                    continue;
                }

                if ((SendPropType) prop.Type == SendPropType.DataTable)
                {
                    var subTable = GetTableByName(prop.DtName);
                    if (subTable != null)
                    {
                        if (((SendPropFlag) prop.Flags & SendPropFlag.COLLAPSIBLE) != 0)
                        {
                            GatherProps_IterateProps(subTable, classId, props);
                        }
                        else
                        {
                            GatherProps(subTable, classId);
                        }
                    }
                }
                else
                {
                    props.Add((SendPropType) prop.Type == SendPropType.Array
                        ? new FlattenedProperty(prop, table.Props[i - 1])
                        : new FlattenedProperty(prop, null));
                }
            }
        }

        private void GatherProps(CSVCMsgSendTable table, int classId)
        {
            var tempProps = new List<FlattenedProperty>();
            GatherProps_IterateProps(table, classId, tempProps);

            var flattened = _serverClasses[classId].FlattenedProps;
            for (var i = 0; i < tempProps.Count; ++i)
            {
                flattened.Add(tempProps[i]);
            }
        }

        private void FlattenDataTable(int classId)
        {
            var svClass = _serverClasses[classId];
            var table = svClass.DataTable;

            _excludes.Clear();
            GatherExcludes(table);

            GatherProps(table, classId);

            svClass.SortFlattenedProps();
        }

        public void HandleDataTables(Stream stream)
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
                svClass.DataTable = GetTableByName(svClass.DataTableName);
                _serverClasses.Add(svClass);
            }

            for (var i = 0; i < svClassCount; ++i)
            {
                FlattenDataTable(i);
            }

            _serverClassBits = 1;
            var temp = svClassCount;
            while ((temp >>= 1) > 0) ++_serverClassBits;
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

        private readonly Dictionary<int, BaseEntity> _entities = new Dictionary<int, BaseEntity>();

        public BaseEntity FindEntity(int entId)
        {
            BaseEntity ent;
            return _entities.TryGetValue(entId, out ent) ? ent : null;
        }

        public TEntity CreateEntity<TEntity>()
            where TEntity : BaseEntity
        {
            return AddEntity<TEntity>(-1, -1, 0);
        }

        public BaseEntity CreateEntity(IEnumerable<KeyValuePair<string, EntValue>> keyVals)
        {
            string className = null;

            foreach (var keyVal in keyVals)
            {
                switch (keyVal.Key)
                {
                    case "classname":
                        className = (string) keyVal.Value;
                        break;
                }
            }

            BaseEntity inst;
            HammerNameCtor ctor;
            if (_sHammerNameCtors.TryGetValue(className, out ctor)) inst = ctor(this);
            else return null;

            inst.ReadKeyVals(keyVals);
            return inst;
        }

        private BaseEntity AddEntity(int entId, int classId, uint serialNum)
        {
            if (classId >= 0)
            {
                var svClass = _serverClasses[classId];
                ClassNameCtor ctor;
                if (_sClassNameCtors.TryGetValue(svClass.Name, out ctor))
                {
                    return ctor(this, entId, classId, serialNum);
                }
            }

            return AddEntity<BaseEntity>(entId, classId, serialNum);
        }

        private TEntity AddEntity<TEntity>(int entId, int classId, uint serialNum)
            where TEntity : BaseEntity
        {
            var ent = FindEntity(entId);
            if (ent == null)
            {
                ent = new GameObject(classId >= 0 ? _serverClasses[classId].Name : typeof(TEntity).Name).AddComponent<TEntity>();
                ent.World = (World) FindEntity(0);
                ent.Id = entId;

                if (entId >= 0) _entities.Add(entId, ent);
            }
            else if (!(ent is TEntity))
            {
                throw new Exception("Existing entity found, but of the wrong class.");
            }

            ent.ClassId = classId;
            ent.SerialNum = serialNum;

            return (TEntity) ent;
        }

        private void RemoveEntity(int entId)
        {
            var ent = _entities[entId];
            Destroy(ent.gameObject);

            _entities.Remove(entId);
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
        private void ReadNewEntity(BitBuffer bitBuffer, BaseEntity ent)
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

            var props = _serverClasses[ent.ClassId].FlattenedProps;
            for (var i = 0; i < _fieldIndices.Count; ++i)
            {
                var fieldIndex = _fieldIndices[i];
                var prop = props[fieldIndex];
                ent.ReadProperty(bitBuffer, prop, 0);
            }
        }

        public void HandlePacket(CSVCMsgPacketEntities message)
        {
            var headerCount = message.UpdatedEntries;
            var updateType = UpdateType.PreserveEnt;
            var updateFlags = UpdateFlags.Zero;
            var headerBase = -1;
            var newEntity = -1;

            var bitBuffer = new BitBuffer(message.EntityData);

            while (updateType < UpdateType.Finished)
            {
                var isEntity = --headerCount >= 0;
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
                    else if ((updateFlags & UpdateFlags.LeavePvs) == UpdateFlags.LeavePvs) updateType = UpdateType.LeavePVS;
                    else updateType = UpdateType.DeltaEnt;

                    switch (updateType)
                    {
                        case UpdateType.EnterPVS:
                            {
                                var classId = bitBuffer.ReadUBitLong(_serverClassBits);
                                var serialNum = bitBuffer.ReadUBitLong(10);
                                var ent = AddEntity(newEntity, (int) classId, serialNum);

                                var baselineTable = _netClient.GetStringTable("instancebaseline");

                                byte[] baselineData;
                                if (baselineTable.TryGetUserData(ent.ClassId.ToString(), out baselineData))
                                {
                                    ReadNewEntity(new BitBuffer(baselineData), ent);
                                }

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
                                Debug.Assert(ent != null, "Attempted to update a non-existent entity!");
                                if (ent == null) return;
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

        private NetClient _netClient;

        [UsedImplicitly]
        private void Awake()
        {
            var ents = FindObjectsOfType<BaseEntity>();
            var world = (World) ents.First(x => x is World);

            _netClient = world.NetClient;

            foreach (var ent in ents)
            {
                ent.World = world;

                if (ent.Id >= 0)
                {
                    _entities.Add(ent.Id, ent);
                }
            }
        }
    }
}
