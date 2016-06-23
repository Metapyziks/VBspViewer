using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JetBrains.Annotations;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Behaviours
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PropStatic : MonoBehaviour
    {
        private string _curModel;

        private MeshRenderer _renderer;
        private MeshFilter _meshFilter;

        public MeshRenderer Renderer { get { return _renderer; } }

        public string Model;
        public string VertexLighting;
        public string Unknown;
        public List<string> Flags;

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
                var mdl = Map.Resources.LoadMdl(mdlPath);

                _meshFilter.sharedMesh = mdl.GetMesh(0, VertexLighting);

                var mats = new Material[_meshFilter.sharedMesh.subMeshCount];
                for (var i = 0; i < mats.Length; ++i)
                {
                    var mat = Map.Resources.LoadVmt(mdl.GetMaterialName(0, i));
                    mats[i] = mat.GetMaterial(Map.Resources);
                }

                _renderer.sharedMaterials = mats;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _meshFilter.sharedMesh = null;
            }
        }

        public void SetFlags(int flags)
        {
            if (Flags == null) Flags = new List<string>();
            else Flags.Clear();

            foreach (var flag in Enum.GetValues(typeof(StaticPropFlag)).Cast<StaticPropFlag>())
            {
                if (((StaticPropFlag) flags & flag) != flag) continue;

                Flags.Add(flag.ToString());
            }
        }

        [UsedImplicitly]
        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();

            SetModel(Model);
        }
    }
}