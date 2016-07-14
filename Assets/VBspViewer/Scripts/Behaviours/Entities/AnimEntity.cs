using System;
using JetBrains.Annotations;
using UnityEngine;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Mdl;
using VBspViewer.Importing.Vmt;

namespace VBspViewer.Behaviours.Entities
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AnimEntity : BaseEntity
    {
        private string _curModel;
        private int _curModelIndex = -1;
        private bool _foundAmbientLighting;

        private MeshRenderer _renderer;
        private MeshFilter _meshFilter;

        public MeshRenderer Renderer { get { return _renderer; } }

        public string Model;
        public int ModelIndex = -1;

        public bool UseLeafAmbientLighting = true;

        protected virtual Mesh OnGetMesh(MdlFile mdl, int lod)
        {
            return mdl.GetMesh(lod, null);
        }

        public void SetModelIndex(int index)
        {
            if (_curModelIndex == index) return;
            _curModelIndex = ModelIndex = index;

            var modelTable = World.NetClient.GetStringTable("modelprecache");
            SetModel(modelTable[index]);
        }

        public void SetModel(string mdlPath)
        {
            if (StringComparer.InvariantCultureIgnoreCase.Equals(mdlPath, _curModel)) return;
            _curModel = Model = mdlPath;

            if (string.IsNullOrEmpty(mdlPath))
            {
                _meshFilter.sharedMesh = null;
                return;
            }

            try
            {
                var mdl = World.Resources.LoadMdl(mdlPath);

                _meshFilter.sharedMesh = OnGetMesh(mdl, 0);

                var mats = new Material[_meshFilter.sharedMesh.subMeshCount];

                for (var i = 0; i < mats.Length; ++i)
                {
                    try
                    {
                        var matName = mdl.GetMaterialName(0, i);
                        var mat = World.Resources.LoadVmt(matName);
                        mats[i] = mat.GetMaterial(World.Resources);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        mats[i] = VmtFile.GetDefaultMaterial(false);
                    }
                }

                _renderer.sharedMaterials = mats;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _meshFilter.sharedMesh = null;
            }
        }

        protected override void OnAwake()
        {
            base.OnAwake();

            _renderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (string.IsNullOrEmpty(_curModel)) SetModel(Model);
        }

        private void UpdateLeafAmbientLighting()
        {
            Vector3 cubePos;
            var nearest = World.BspFile.GetNearestAmbientLightCube(transform.position, out cubePos);
            if (nearest == null) return;

            var debug = new GameObject("Ambient");
            debug.transform.SetParent(transform, false);
            debug.transform.position = cubePos;

            var mat = _renderer.material;

            mat.EnableKeyword("AMBIENT_CUBE");
            for (var i = 0; i < 6; ++i)
            {
                mat.SetColor("_AmbientCube" + i, nearest[i]);
            }
        }

        protected override void OnReadProperty<TVal>(string name, int index, TVal value)
        {
            switch (name)
            {
                case "m_nModelIndex":
                    ModelIndex = (int) (object) value;
                    break;
                default:
                    base.OnReadProperty(name, index, value);
                    break;
            }
        }

        protected override void OnKeyVal(string key, EntValue val)
        {
            switch (key)
            {
                case "model":
                    Model = (string) val;
                    break;
                default:
                    base.OnKeyVal(key, val);
                    break;
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (_curModelIndex != ModelIndex) SetModelIndex(ModelIndex);
            else if (!StringComparer.InvariantCultureIgnoreCase.Equals(Model, _curModel)) SetModel(Model);

            if (UseLeafAmbientLighting && !_foundAmbientLighting)
            {
                _foundAmbientLighting = true;
                UpdateLeafAmbientLighting();
            }
        }
    }
}