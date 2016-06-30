using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VBspViewer.Importing.VBsp.Structures;
using PrimitiveType = UnityEngine.PrimitiveType;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(ClassName = "CCSPlayer")]
    public class CSPlayer : BaseEntity
    {
        [UsedImplicitly]
        private void Awake()
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(transform, false);
            capsule.transform.localPosition = Vector3.up * 0.5f;
            capsule.GetComponent<MeshRenderer>().material.color = Color.red;
        }
    }
}
