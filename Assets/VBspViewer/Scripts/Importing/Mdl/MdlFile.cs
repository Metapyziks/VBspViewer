using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.Mdl
{
    public class MdlFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VertexFileHeader
        {
            public int Id;
            public int Version;
            public long Checksum;
            public int NumLods;
            public int NumLodVertices0;
            public int NumLodVertices1;
            public int NumLodVertices2;
            public int NumLodVertices3;
            public int NumLodVertices4;
            public int NumLodVertices5;
            public int NumLodVertices6;
            public int NumLodVertices7;
            public int FixupTableStart;
            public int VertexDataStart;
            public int TangentDataStart;

            public int GetNumLodVertices(int lod)
            {
                switch (lod)
                {
                    case 0: return NumLodVertices0;
                    case 1: return NumLodVertices1;
                    case 2: return NumLodVertices2;
                    case 3: return NumLodVertices3;
                    case 4: return NumLodVertices4;
                    case 5: return NumLodVertices5;
                    case 6: return NumLodVertices6;
                    case 7: return NumLodVertices7;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MaterialReplacementListHeader
        {
            public int NumReplacements;
            public int ReplacementOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BodyPartHeader
        {
            public int NumModels;
            public int ModelOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ModelHeader
        {
            public int NumLods;
            public int LodOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ModelLodHeader
        {
            public int NumMeshes;
            public int MeshOffset;
            public float SwitchPoint;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MeshHeader
        {
            public int NumStripGroups;
            public int StripGroupHeaderOffset;
            public byte MeshFlags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StripGroupHeader
        {
            public int NumVerts;
            public int VertOffset;

            public int NumIndices;
            public int IndexOffset;

            public int NumStrips;
            public int StripOffset;
        }

        private enum StripHeaderFlags : byte
        {
            IsTriList = 1,
            IsTriStrip = 2
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StripHeader
        {
            public int NumIndices;
            public int IndexOffset;

            public int NumVerts;
            public int VertOffset;

            public short NumBones;
            public StripHeaderFlags Flags;

            public int NumBoneStateChanges;
            public int BoneStateChangeOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct OptimizedVertex
        {
            public byte BoneWeightIndex0;
            public byte BoneWeightIndex1;
            public byte BoneWeightIndex2;
            public byte NumBones;

            public ushort OrigMeshVertId;

            public sbyte BoneId0;
            public sbyte BoneId1;
            public sbyte BoneId2;
        }

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

        private StudioVertex[] _vertices;
        private StudioVertex[] GetVertices()
        {
            if (_vertices != null) return _vertices;

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

                reader.BaseStream.Seek(vertexDataStart, SeekOrigin.Begin);

                var vertList = new List<StudioVertex>();

                ReadLumpWrapper<StudioVertex>.ReadLumpFromStream(reader.BaseStream, (tangentDataStart - vertexDataStart)/Marshal.SizeOf(typeof(StudioVertex)), vertList);
                _vertices = vertList.ToArray();
            }

            return _vertices;
        }

        private int[][] GetTriangles(int lodLevel = 0)
        {
            var filePath = _baseFilename + ".dx90.vtx";

            var outIndices = new List<List<int>>();

            using (var stream = _loader.OpenFile(filePath))
            using (var reader = new BinaryReader(stream))
            {
                var version = reader.ReadInt32();

                Debug.Assert(version == 7);

                var vertCacheSize = reader.ReadInt32();
                var maxBonesPerStrip = reader.ReadUInt16();
                var maxBonesPerTri = reader.ReadUInt16();
                var maxBonesPerVert = reader.ReadInt32();

                var checksum = reader.ReadInt32();

                var numLods = reader.ReadInt32();
                var matReplacementListOffset = reader.ReadInt32();

                var numBodyParts = reader.ReadInt32();
                var bodyPartOffset = reader.ReadInt32();
                
                var verts = new List<OptimizedVertex>();
                var indices = new List<ushort>();

                reader.BaseStream.Seek(bodyPartOffset, SeekOrigin.Begin);
                ReadLumpWrapper<BodyPartHeader>.ReadLumpFromStream(reader.BaseStream, numBodyParts, bodyPart =>
                {
                    reader.BaseStream.Seek(bodyPart.ModelOffset, SeekOrigin.Current);
                    ReadLumpWrapper<ModelHeader>.ReadLumpFromStream(reader.BaseStream, bodyPart.NumModels, model =>
                    {
                        reader.BaseStream.Seek(model.LodOffset, SeekOrigin.Current);

                        var lodIndex = 0;
                        ReadLumpWrapper<ModelLodHeader>.ReadLumpFromStream(reader.BaseStream, model.NumLods, lod =>
                        {
                            if (lodIndex++ != lodLevel) return;

                            var meshIndex = 0;
                            var skip = 0;

                            reader.BaseStream.Seek(lod.MeshOffset, SeekOrigin.Current);
                            ReadLumpWrapper<MeshHeader>.ReadLumpFromStream(reader.BaseStream, lod.NumMeshes, mesh =>
                            {
                                List<int> meshIndices;
                                if (outIndices.Count <= meshIndex) outIndices.Add(meshIndices = new List<int>());
                                else meshIndices = outIndices[meshIndex];
                                
                                reader.BaseStream.Seek(mesh.StripGroupHeaderOffset, SeekOrigin.Current);
                                ReadLumpWrapper<StripGroupHeader>.ReadLumpFromStream(reader.BaseStream, mesh.NumStripGroups, stripGroup =>
                                {
                                    verts.Clear();
                                    indices.Clear();

                                    var start = reader.BaseStream.Position;
                                    reader.BaseStream.Seek(start + stripGroup.VertOffset, SeekOrigin.Begin);
                                    ReadLumpWrapper<OptimizedVertex>.ReadLumpFromStream(reader.BaseStream,
                                        stripGroup.NumVerts, verts);

                                    reader.BaseStream.Seek(start + stripGroup.IndexOffset, SeekOrigin.Begin);
                                    ReadLumpWrapper<ushort>.ReadLumpFromStream(reader.BaseStream,
                                        stripGroup.NumIndices, indices);
                                    
                                    reader.BaseStream.Seek(start + stripGroup.StripOffset, SeekOrigin.Begin);
                                    ReadLumpWrapper<StripHeader>.ReadLumpFromStream(reader.BaseStream, stripGroup.NumStrips, strip =>
                                    {
                                        Debug.Assert(strip.Flags == StripHeaderFlags.IsTriList);
                                        
                                        for (var i = 0; i < strip.NumIndices; ++i)
                                        {
                                            var index = indices[strip.IndexOffset + i];
                                            var vert = verts[index];

                                            meshIndices.Add(strip.VertOffset + vert.OrigMeshVertId + skip);
                                        }
                                    });

                                    // Why?
                                    skip += verts.Max(x => x.OrigMeshVertId) + 1;
                                });

                                meshIndex += 1;
                            });
                        });
                    });
                });
            }

            return outIndices.Select(x => x.ToArray()).ToArray();
        }

        [ThreadStatic]
        private static List<Vector3> _sVertices;
        [ThreadStatic]
        private static List<Vector3> _sNormals;

        public Mesh GetMesh(int lodLevel)
        {
            Mesh mesh;
            if (_lods.TryGetValue(lodLevel, out mesh)) return mesh;

            mesh = new Mesh();
            
            using (Profiler.Begin("BuildPropMesh"))
            { 
                var verts = GetVertices();
                var indices = GetTriangles(lodLevel);
                
                if (_sVertices == null) _sVertices = new List<Vector3>();
                else _sVertices.Clear();

                if (_sNormals == null) _sNormals = new List<Vector3>();
                else _sNormals.Clear();

                var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-90f, Vector3.up), Vector3.one);

                for (var i = 0; i < verts.Length; ++i)
                {
                    _sVertices.Add(transform*((Vector3) verts[i].Position*VBspFile.SourceToUnityUnits));
                    _sNormals.Add(transform*(Vector3) verts[i].Normal);
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
    }
}
