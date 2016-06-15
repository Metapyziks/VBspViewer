using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.Mdl
{
    public partial class MdlFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VertexFixup
        {
            public int Lod;
            public int SourceVertexId;
            public int NumVertices;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StudioVertex
        {
            public StudioBoneWeight BoneWeights;
            public Vector Position;
            public Vector Normal;
            public float TexCoordX;
            public float TexCoordY;

            public override string ToString()
            {
                return string.Format("{0}, {1}, ({2}, {3})", Position, Normal, TexCoordX, TexCoordY);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StudioBoneWeight
        {
            public float Weight0;
            public float Weight1;
            public float Weight2;
            public byte Bone0;
            public byte Bone1;
            public byte Bone2;
            public byte NumBones;
        }

        private StudioVertex[][] _vertices;
        private StudioVertex[] GetVertices(int lod)
        {
            if (_vertices != null) return _vertices[lod];

            var filePath = _baseFilename + ".vvd";

            using (var stream = _loader.OpenFile(filePath))
            using (var reader = new BinaryReader(stream))
            {
                var id = reader.ReadInt32();
                var version = reader.ReadInt32();

                Debug.Assert(id == 0x56534449);
                Debug.Assert(version == 4);

                reader.ReadInt32();

                var numLods = reader.ReadInt32();
                var numLodVerts = new int[8];

                for (var i = 0; i < numLodVerts.Length; ++i)
                {
                    numLodVerts[i] = reader.ReadInt32();
                }

                var numFixups = reader.ReadInt32();
                var fixupTableStart = reader.ReadInt32();
                var vertexDataStart = reader.ReadInt32();
                var tangentDataStart = reader.ReadInt32();

                var fixupList = new List<VertexFixup>();
                var vertList = new List<StudioVertex>();

                if (numFixups > 0)
                {
                    reader.BaseStream.Seek(fixupTableStart, SeekOrigin.Begin);
                    ReadLumpWrapper<VertexFixup>.ReadLumpFromStream(reader.BaseStream, numFixups, fixupList);
                }

                reader.BaseStream.Seek(vertexDataStart, SeekOrigin.Begin);
                ReadLumpWrapper<StudioVertex>.ReadLumpFromStream(reader.BaseStream, (tangentDataStart - vertexDataStart) / Marshal.SizeOf(typeof(StudioVertex)), vertList);

                _vertices = new StudioVertex[numLods][];

                var lodVerts = new List<StudioVertex>();

                for (var i = 0; i < numLods; ++i)
                {
                    if (numFixups == 0)
                    {
                        _vertices[i] = vertList.Take(numLodVerts[i]).ToArray();
                        continue;
                    }

                    lodVerts.Clear();

                    foreach (var vertexFixup in fixupList)
                    {
                        if (vertexFixup.Lod >= i)
                        {
                            lodVerts.AddRange(vertList.Skip(vertexFixup.SourceVertexId).Take(vertexFixup.NumVertices));
                        }
                    }

                    _vertices[i] = lodVerts.ToArray();
                }
            }

            return _vertices[lod];
        }
    }
}