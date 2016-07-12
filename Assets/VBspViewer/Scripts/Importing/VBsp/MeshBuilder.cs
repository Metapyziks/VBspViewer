using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VBspViewer.Importing.Vmt;
using PrimitiveType = VBspViewer.Importing.VBsp.Structures.PrimitiveType;

namespace VBspViewer.Importing.VBsp
{
    public class MeshBuilder
    {
        private struct MeshVertex : IEquatable<MeshVertex>
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector2 TextureUv;
            public readonly Vector2 LightmapUv;

            public MeshVertex(Vector3 position, Vector3 normal, Vector2 textureUv, Vector2 lightmapUv)
            {
                Position = position;
                Normal = normal;
                TextureUv = textureUv;
                LightmapUv = lightmapUv;
            }

            public bool Equals(MeshVertex other)
            {
                return Position == other.Position && Normal == other.Normal && TextureUv == other.TextureUv && LightmapUv == other.LightmapUv;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshVertex && Equals((MeshVertex) obj);
            }

            public override int GetHashCode()
            {
                return Position.GetHashCode();
            }
        }

        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _textureUvs = new List<Vector2>();
        private readonly List<Vector2> _lightmapUvs = new List<Vector2>(); 
        private readonly Dictionary<MeshVertex, int> _vertDict = new Dictionary<MeshVertex, int>();

        private readonly List<List<int>> _indicesPool = new List<List<int>>(); 

        private readonly Dictionary<Material, List<int>> _materials = new Dictionary<Material, List<int>>(); 

        private List<int> _indices;
        private readonly List<int> _faceIndices = new List<int>();

        public Vector3 Offset { get; set; }

        public void Clear()
        {
            _verts.Clear();
            _normals.Clear();
            _textureUvs.Clear();
            _lightmapUvs.Clear();
            _vertDict.Clear();

            _indicesPool.AddRange(_materials.Values);

            _materials.Clear();
            _faceIndices.Clear();

            _indices = null;
        }

        public void StartFace(Material mat)
        {
            List<int> indices;
            if (_materials.TryGetValue(mat, out indices))
            {
                _indices = indices;
                return;
            }

            if (_indicesPool.Count > 0)
            {
                _indices = _indicesPool[_indicesPool.Count - 1];
                _indicesPool.RemoveAt(_indicesPool.Count - 1);
                _indices.Clear();
            }
            else
            {
                _indices = new List<int>();
            }

            _materials.Add(mat, _indices);
        }

        public void AddPrimitive(PrimitiveType type)
        {
            AddPrimitive(type, Enumerable.Range(0, _faceIndices.Count));
        }

        public void AddPrimitive(PrimitiveType type, IEnumerable<int> indices)
        {
            switch (type)
            {
                case PrimitiveType.TriangleStrip:
                    var first = indices.FirstOrDefault();
                    var prev = default(int);

                    foreach (var index in indices)
                    {
                        _indices.Add(_faceIndices[first]);
                        _indices.Add(_faceIndices[prev]);
                        _indices.Add(_faceIndices[index]);

                        prev = index;
                    }
                    break;
                case PrimitiveType.TriangleList:
                    foreach (var index in indices)
                    {
                        _indices.Add(_faceIndices[index]);
                    }
                    break;
            }
        }

        public void EndFace()
        {
            _faceIndices.Clear();
        }

        public void AddVertex(Vector3 pos, Vector3 normal, Vector2 textureUv, Vector2 lightmapUv)
        {
            pos += Offset;

            var meshVert = new MeshVertex(pos, normal, textureUv, lightmapUv);

            int vertIndex;
            if (!_vertDict.TryGetValue(meshVert, out vertIndex))
            {
                vertIndex = _verts.Count;

                _verts.Add(pos);
                _normals.Add(normal);
                _textureUvs.Add(textureUv);
                _lightmapUvs.Add(lightmapUv);
                _vertDict.Add(meshVert, vertIndex);
            }

            _faceIndices.Add(vertIndex);
        }

        public void AddVertex(Vector3 vert)
        {
            AddVertex(vert, Vector3.up, Vector2.zero,  Vector2.zero);
        }

        public void CopyToMesh(Mesh mesh)
        {
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _textureUvs);
            mesh.SetUVs(1, _lightmapUvs);
            mesh.subMeshCount = _materials.Count;

            var index = 0;
            foreach (var material in _materials)
            {
                mesh.SetIndices(material.Value.ToArray(), MeshTopology.Triangles, index++);
            }
        }

        public Material[] GetMaterials()
        {
            var arr = new Material[_materials.Count];

            var index = 0;
            foreach (var material in _materials)
            {
                arr[index++] = material.Key;
            }

            return arr;
        }
    }
}
