using UnityEngine;
using System.Collections;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(HammerName = "prop_door_rotating", ClassName = "CPropDoorRotating")]
    public class PropDoorRotating : AnimEntity
    {
        protected override void OnReadProperty<TVal>(string name, int index, TVal value)
        {
            base.OnReadProperty(name, index, value);
        }
    }
}
