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
        }

        private Color[] GetVertexLighting(string filePath, int lodLevel)
        {
            var indexMap = GetVertIndexMap(lodLevel);
            var verts = GetVertices(lodLevel);

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

                var meshHeaders = new List<VhvMeshHeader>();
                ReadLumpWrapper<VhvMeshHeader>.ReadLumpFromStream(reader.BaseStream, meshCount, meshHeaders);

                var sampleList = new List<Color32>();
                var output = new Color[verts.Length];

                const float scale = 1f/255f;
                const float exp = 1f;//1f/2.2f;

                var meshIndex = 0;
                foreach (var meshHeader in meshHeaders)
                {
                    if (meshHeader.Lod != lodLevel) continue;
                    if (meshHeader.VertCount == 0) continue;
                    if (meshIndex >= indexMap.Length) break;

                    sampleList.Clear();

                    reader.BaseStream.Seek(meshHeader.VertOffset, SeekOrigin.Begin);
                    ReadLumpWrapper<Color32>.ReadLumpFromStream(reader.BaseStream, meshHeader.VertCount, sampleList);

                    var map = indexMap[meshIndex];

                    for (var i = 0; i < sampleList.Count; ++i)
                    {
                        var sample = sampleList[i];
                        output[map[i]] = new Color(Mathf.Pow(sample.b * scale, exp), Mathf.Pow(sample.g * scale, exp), Mathf.Pow(sample.r * scale, exp), 1f);
                    }

                    meshIndex += 1;
                }

                return output;
            }
        }
    }
}
