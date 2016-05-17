using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VBspViewer.Importing.Structures;
using PrimitiveType = VBspViewer.Importing.Structures.PrimitiveType;

namespace VBspViewer.Importing
{
    public class MeshBuilder
    {
        private struct MeshVertex : IEquatable<MeshVertex>
        {
            public readonly Vector Position;
            public readonly Vector Normal;

            public MeshVertex(Vector position, Vector normal)
            {
                Position = position;
                Normal = normal;
            }

            public bool Equals(MeshVertex other)
            {
                return Position.Equals(other.Position) && Normal.Equals(other.Normal);
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
        private readonly Dictionary<MeshVertex, int> _vertDict = new Dictionary<MeshVertex, int>();

        private readonly List<int> _indices = new List<int>();
        private readonly List<int> _faceIndices = new List<int>();

        public void StartFace() { }

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

        public void AddVertex(Vector pos, Vector normal)
        {
            const float inchesToMetre = 1f/39.3701f;

            var meshVert = new MeshVertex(pos, normal);

            int vertIndex;
            if (!_vertDict.TryGetValue(meshVert, out vertIndex))
            {
                vertIndex = _verts.Count;

                _verts.Add(((Vector3) pos) * inchesToMetre);
                _normals.Add(normal);
                _vertDict.Add(meshVert, vertIndex);
            }

            _faceIndices.Add(vertIndex);
        }

        public void CopyToMesh(Mesh mesh)
        {
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetIndices(_indices.ToArray(), MeshTopology.Triangles, 0);
        }
    }
}
