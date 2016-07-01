using JetBrains.Annotations;
using UnityEngine;
using VBspViewer.Importing.VBsp;
using PrimitiveType = UnityEngine.PrimitiveType;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(ClassName = "CCSPlayer")]
    public class CSPlayer : BaseEntity
    {
        private Camera _camera;
        private NetClient.PlayerInfo _info;

        public string Nick;

        protected override void OnAwake()
        {
            base.OnAwake();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(transform, false);
            capsule.transform.localScale = new Vector3(16f, 36f, 16f) * VBspFile.SourceToUnityUnits;
            capsule.transform.localPosition = Vector3.up * 36f * VBspFile.SourceToUnityUnits;
            capsule.GetComponent<MeshRenderer>().material.color = Color.red;

            _camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.transform.localPosition = Vector3.up*64f*VBspFile.SourceToUnityUnits;
            _camera.enabled = false;
        }
        
        protected override void OnReadProperty<TVal>(string name, int index, TVal value)
        {
            switch (name)
            {
                case "m_angEyeAngles[0]":
                {
                    var ang = _camera.transform.localEulerAngles;
                    ang.x = (float) (object) value;
                    _camera.transform.localEulerAngles = ang;
                    return;
                }
                case "m_angEyeAngles[1]":
                {
                    var ang = _camera.transform.localEulerAngles;
                    ang.y = 90f - (float) (object) value;
                    _camera.transform.localEulerAngles = ang;
                    return;
                }
                default:
                    base.OnReadProperty(name, index, value);
                    return;
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (_info != null)
            {
                Nick = _info.Name;
                return;
            }

            _info = World.NetClient.GetPlayerInfoFromEntityIndex(Id);
        }
    }
}
