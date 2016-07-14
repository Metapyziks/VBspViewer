using UnityEngine;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Mdl;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(HammerName = "prop_static")]
    public class PropStatic : AnimEntity
    {
        public string VertexLighting;

        public PropStatic()
        {
            UseLeafAmbientLighting = false;
        }

        protected override void OnKeyVal(string key, EntValue val)
        {
            switch (key)
            {
                case "vlighting":
                    VertexLighting = (string) val;
                    break;
                default:
                    base.OnKeyVal(key, val);
                    break;
            }
        }

        protected override Mesh OnGetMesh(MdlFile mdl, int lod)
        {
            return mdl.GetMesh(lod, VertexLighting);
        }

        protected override void OnAwake()
        {
            base.OnAwake();

            gameObject.isStatic = true;
        }
    }
}