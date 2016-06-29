using System;
using System.Collections.Generic;
using UnityEngine;
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
