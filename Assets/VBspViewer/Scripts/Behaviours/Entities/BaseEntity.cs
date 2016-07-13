using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem.Generated;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Behaviours.Entities
{
    public class BaseEntity : MonoBehaviour
    {
        protected sealed class DtPropAttribute : Attribute
        {
            public string PropName { get; set; }

            public DtPropAttribute(string propName)
            {
                PropName = propName;
            }
        }

        protected internal World World { get; internal set; }

        public int Id = -1;
            
        [HideInInspector] public int ClassId = -1;
        [HideInInspector] public uint SerialNum = 0;

        [HideInInspector] public int CellX = 512;
        [HideInInspector] public int CellY = 512;
        [HideInInspector] public int CellZ = 512;

        [HideInInspector] public Vector3 Origin;
        [HideInInspector] public Vector3 Angles;

        internal void ReadProperty(BitBuffer bitBuffer, FlattenedProperty flatProp, int index)
        {
            var prop = flatProp.Property;

            switch ((SendPropType) prop.Type)
            {
                case SendPropType.Int:
                    ReadProperty(prop, index, PropertyDecode.DecodeInt(bitBuffer, prop));
                    break;
                case SendPropType.Float:
                    ReadProperty(prop, index, PropertyDecode.DecodeFloat(bitBuffer, prop));
                    break;
                case SendPropType.Vector:
                    ReadProperty(prop, index, PropertyDecode.DecodeVector(bitBuffer, prop));
                    break;
                case SendPropType.VectorXY:
                    ReadProperty(prop, index, PropertyDecode.DecodeVectorXY(bitBuffer, prop));
                    break;
                case SendPropType.String:
                    ReadProperty(prop, index, PropertyDecode.DecodeString(bitBuffer, prop));
                    break;
                case SendPropType.Array:
                {
                    var maxElems = prop.NumElements;
                    var bits = 1;
                    while ((maxElems >>= 1) != 0) ++bits;

                    var elems = bitBuffer.ReadUBitLong(bits);

                    for (var i = 0; i < elems; ++i)
                    {
                        var temp = new FlattenedProperty(flatProp.ArrayElementProperty, null);
                        ReadProperty(bitBuffer, temp, i);
                    }

                    break;
                }
                case SendPropType.DataTable:
                    break;
                case SendPropType.Int64:
                    ReadProperty(prop, index, PropertyDecode.DecodeInt64(bitBuffer, prop));
                    break;
            }
        }

        internal void ReadProperty<TVal>(CSVCMsgSendTable.SendpropT prop, int index, TVal value)
        {
            //Debug.LogFormat("{0}: {1}", prop.VarName, value);
            
            OnReadProperty(prop.VarName, index, value);
        }

        protected virtual void OnReadProperty<TVal>(string name, int index, TVal value)
        {
            switch (name)
            {
                case "m_cellX":
                    CellX = (int) (object) value;
                    UpdatePosition();
                    return;
                case "m_cellY":
                    CellZ = (int) (object) value;
                    UpdatePosition();
                    return;
                case "m_cellZ":
                    CellY = (int) (object) value;
                    UpdatePosition();
                    return;
                case "m_angRotation":
                    if (value is Vector3)
                    {
                        var val = (Vector3) (object) value;
                        Angles = new Vector3(val.x, -val.z, val.y);
                    }
                    UpdatePosition();
                    return;
                case "m_vecOrigin":
                    if (value is Vector2)
                    {
                        var vec2Val = (Vector2) (object) value;
                        Origin = new Vector3(vec2Val.x, Origin.y, vec2Val.y);
                    }
                    else if (value is Vector3)
                    {
                        Origin = (Vector3) (object) value;
                    }
                    UpdatePosition();
                    return;
                case "m_vecOrigin[2]":
                    if (value is float)
                    {
                        var floatVal = (float) (object) value;
                        Origin = new Vector3(Origin.x, floatVal, Origin.z);
                    }
                    UpdatePosition();
                    return;
            }
        }

        private void UpdatePosition()
        {
            transform.localPosition = (new Vector3(CellX - 512, CellY - 512, CellZ - 512) * 32f
                + Origin) * VBspFile.SourceToUnityUnits;
            transform.localRotation = Quaternion.Euler(Angles);
        }

        internal void ReadKeyVals(IEnumerable<KeyValuePair<string, EntValue>> keyVals)
        {
            foreach (var keyVal in keyVals)
            {
                OnKeyVal(keyVal.Key, keyVal.Value);
            }
        }

        protected virtual void OnKeyVal(string key, EntValue val)
        {
            switch (key)
            {
                case "targetname":
                    name = (string) val;
                    break;
                case "origin":
                    Origin = (Vector3) val;
                    UpdatePosition();
                    break;
                case "angles":
                    Angles = ((Quaternion) val).eulerAngles;
                    UpdatePosition();
                    break;
            }
        }

        [UsedImplicitly]
        private void Awake()
        {
            OnAwake();
        }

        protected virtual void OnAwake() { }

        [UsedImplicitly]
        private void Start()
        {
            OnStart();
        }

        protected virtual void OnStart() { }
    }
}
