using System;
using System.Collections.Generic;
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

        protected internal EntityManager Entities { get; internal set; }

        public int Id = -1;

        [HideInInspector] public int ClassId = -1;
        [HideInInspector] public uint SerialNum = 0;

        [DtProp("m_vecOrigin")]
        public Vector3 Origin
        {
            get { return transform.position; }
            set { transform.position = value; }
        }

        //[DtProp("m_vecAngles")]
        public Quaternion Angles
        {
            get { return transform.rotation; }
            set { transform.rotation = value; }
        }

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
            Debug.LogFormat("{0}: {1}", prop.VarName, value);
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
                    Origin = (Vector3) val*VBspFile.SourceToUnityUnits;
                    break;
                case "angles":
                    Angles = (Quaternion) val;
                    break;
            }
        }
    }
}
