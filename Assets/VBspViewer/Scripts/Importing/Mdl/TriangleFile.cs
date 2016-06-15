using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Importing.Mdl
{
    public partial class MdlFile
    {
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
    }
}