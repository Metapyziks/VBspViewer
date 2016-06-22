using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Importing.Mdl
{
    public partial class MdlFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VhvMeshHeader
        {
            public int Lod;
            public int VertCount;
            public int VertOffset;
            public int Unused0;
            public int Unused1;
            public int Unused2;
            public int Unused3;
        }

        private interface IVertexData
        {
            Color GetVertexColor();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VertexData4 : IVertexData
        {
            public byte B;
            public byte G;
            public byte R;
            public byte A;

            public Color GetVertexColor()
            {
                return new Color32(R, G, B, A);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VertexData2 : IVertexData
        {
            public VertexData4 Color;
            public int Unknown0;
            public int Unknown1;
            
            public Color GetVertexColor()
            {
                return Color.GetVertexColor();
            }
        }

        private Color[] GetVertexLighting(string filePath, int lodLevel)
        {
            using (var stream = _loader.OpenFile(filePath))
            using (var reader = new BinaryReader(stream))
            {
                var version = reader.ReadInt32();

                Debug.Assert(version == 2);
                
                var checksum = reader.ReadInt32();
                var vertFlags = reader.ReadUInt32();
                var vertSize = reader.ReadUInt32();
                var vertCount = reader.ReadUInt32();
                var meshCount = reader.ReadInt32();

                reader.ReadInt64(); // Unused
                reader.ReadInt64(); // Unused

                switch (vertFlags)
                {
                    case 2: return ReadVertexSamples<VertexData2>(reader.BaseStream, lodLevel, meshCount);
                    case 4: return ReadVertexSamples<VertexData4>(reader.BaseStream, lodLevel, meshCount);
                    default: throw new NotImplementedException();
                }
            }
        }

        private Color[] ReadVertexSamples<TVertex>(Stream stream, int lodLevel, int meshCount)
            where TVertex : struct, IVertexData
        {
            var indexMap = GetVertIndexMap(lodLevel);
            var verts = GetVertices(lodLevel);

            var meshHeaders = new List<VhvMeshHeader>();
            ReadLumpWrapper<VhvMeshHeader>.ReadLumpFromStream(stream, meshCount, meshHeaders);

            var sampleList = new List<TVertex>();
            var output = new Color[verts.Length];
            
            var meshIndex = 0;
            foreach (var meshHeader in meshHeaders)
            {
                if (meshHeader.Lod != lodLevel) continue;
                if (meshHeader.VertCount == 0) continue;
                if (meshIndex >= indexMap.Length) break;

                sampleList.Clear();

                stream.Seek(meshHeader.VertOffset, SeekOrigin.Begin);
                ReadLumpWrapper<TVertex>.ReadLumpFromStream(stream, meshHeader.VertCount, sampleList);

                var map = indexMap[meshIndex];

                for (var i = 0; i < sampleList.Count; ++i)
                {
                    output[map[i]] = sampleList[i].GetVertexColor();
                }

                meshIndex += 1;
            }

            return output;
        }
    }
}
