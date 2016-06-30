using UnityEngine;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(HammerName = "func_brush", ClassName = "CFuncBrush")]
    public class BrushEntity : BaseEntity
    {
        public int ModelIndex = -1;

        public int CellX = 500;
        public int CellY = 500;
        public int CellZ = 500;
        public Vector3 Origin;

        protected override void OnKeyVal(string key, EntValue val)
        {
            switch (key)
            {
                case "model":
                    var valStr = (string) val;
                    if (valStr.StartsWith("*")) ModelIndex = int.Parse(valStr.Substring(1));
                    break;
                default:
                    base.OnKeyVal(key, val);
                    break;
            }
        }

        protected override void OnReadProperty<TVal>(string name, int index, TVal value)
        {
            switch (name)
            {
                case "m_nModelIndex":
                    ModelIndex = (int) (object) value;
                    return;
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
                case "m_vecOrigin":
                    Origin = (Vector3) (object) value;
                    UpdatePosition();
                    return;
                default:
                    base.OnReadProperty(name, index, value);
                    return;
            }
        }

        private void UpdatePosition()
        {
            transform.position = (new Vector3(CellX - 500, CellY - 500, CellZ - 500) * 32f
                + Origin) * VBspFile.SourceToUnityUnits;
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (ModelIndex < 0) return;

            var world = Entities.FindEntity(0) as World;
            if (world == null || world.BspFile == null) return;

            var meshes = world.BspFile.GenerateMeshes(ModelIndex);
            foreach (var mesh in meshes)
            {
                if (mesh.vertexCount == 0) continue;

                var modelChild = new GameObject("faces", typeof(MeshFilter), typeof(MeshRenderer));
                modelChild.transform.SetParent(transform, false);

                modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                modelChild.GetComponent<MeshRenderer>().sharedMaterial = world.WorldMaterial;
                modelChild.isStatic = ModelIndex == 0;
            }
        }
    }
}
