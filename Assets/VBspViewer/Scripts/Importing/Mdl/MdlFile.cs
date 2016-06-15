using System;
using System.Collections.Generic;
using UnityEngine;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Importing.Mdl
{
    public partial class MdlFile
    {
        private readonly IResourceProvider _loader;
        private readonly string _baseFilename;

        private readonly Dictionary<int, Mesh> _lods = new Dictionary<int, Mesh>(); 

        public int LodCount
        {
            get { return 1; }
        }

        public MdlFile(IResourceProvider loader, string filename)
        {
            _loader = loader;
            _baseFilename = filename.Substring(0, filename.Length - ".mdl".Length);
        }

        [ThreadStatic]
        private static List<Vector3> _sVertices;
        [ThreadStatic]
        private static List<Vector3> _sNormals;

        private Mesh GetBaseMesh(int lodLevel)
        {
            Mesh mesh;
            if (_lods.TryGetValue(lodLevel, out mesh)) return mesh;

            mesh = new Mesh();

            using (Profiler.Begin("BuildPropMesh"))
            {
                var verts = GetVertices(lodLevel);
                var indices = GetTriangles(lodLevel);

                if (_sVertices == null) _sVertices = new List<Vector3>();
                else _sVertices.Clear();

                if (_sNormals == null) _sNormals = new List<Vector3>();
                else _sNormals.Clear();

                var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-90f, Vector3.up), Vector3.one);

                for (var i = 0; i < verts.Length; ++i)
                {
                    _sVertices.Add(transform * ((Vector3) verts[i].Position * VBspFile.SourceToUnityUnits));
                    _sNormals.Add(transform * (Vector3) verts[i].Normal);
                }

                mesh.SetVertices(_sVertices);
                mesh.SetNormals(_sNormals);

                mesh.subMeshCount = indices.Length;

                for (var i = 0; i < indices.Length; ++i)
                {
                    mesh.SetIndices(indices[i], MeshTopology.Triangles, i);
                }

                _lods.Add(lodLevel, mesh);
                return mesh;
            }
        }

        public Mesh GetMesh(int lodLevel, string vertexLightingFile)
        {
            var mesh = GetBaseMesh(lodLevel);
            if (string.IsNullOrEmpty(vertexLightingFile)) return mesh;

            mesh = UnityEngine.Object.Instantiate(mesh);
            mesh.colors = GetVertexLighting(vertexLightingFile, lodLevel);

            return mesh;
        }
    }
}
