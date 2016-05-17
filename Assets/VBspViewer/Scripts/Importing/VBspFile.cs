using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VBspViewer.Importing.Structures;
using Plane = VBspViewer.Importing.Structures.Plane;
using PrimitiveType = VBspViewer.Importing.Structures.PrimitiveType;

namespace VBspViewer.Importing
{
    public partial class VBspFile
    {
        private readonly Header _header;
        
        public VBspFile(Stream stream)
        {
            var reader = new BinaryReader(stream);
            _header = Header.Read(reader);

            var delegates = GetReadLumpDelegates();

            const int bufferSize = 8192;
            var buffer = new byte[bufferSize];

            using (var tempStream = new MemoryStream())
            {
                var tempReader = new BinaryReader(tempStream);

                for (var lumpIndex = 0; lumpIndex < Header.LumpInfoCount; ++lumpIndex)
                {
                    ReadLumpDelegate deleg;
                    if (!delegates.TryGetValue((LumpType) lumpIndex, out deleg)) continue;

                    var info = _header.Lumps[lumpIndex];
                    
                    Debug.LogFormat("{0} Start: 0x{1:x}, Length: 0x{2:x}", (LumpType) lumpIndex, info.Offset, info.Length);

                    tempStream.Seek(0, SeekOrigin.Begin);
                    tempStream.SetLength(0);

                    stream.Seek(info.Offset, SeekOrigin.Begin);

                    for (var total = 0; total < info.Length;)
                    {
                        var toRead = Math.Min(info.Length - total, bufferSize);
                        var read = stream.Read(buffer, 0, toRead);

                        tempStream.Write(buffer, 0, read);

                        total += read;
                    }

                    tempStream.Seek(0, SeekOrigin.Begin);

                    Debug.Assert(tempStream.Length == info.Length);

                    deleg(this, info, tempReader);
                }
            }
        }
        
        [Lump(Type = LumpType.LUMP_VERTEXES)]
        private Vector[] Vertices { get; set; }

        [Lump(Type = LumpType.LUMP_PLANES)]
        private Plane[] Planes { get; set; }

        [Lump(Type = LumpType.LUMP_EDGES)]
        private Edge[] Edges { get; set; }

        [Lump(Type = LumpType.LUMP_SURFEDGES)]
        private int[] SurfEdges { get; set; }
        
        [Lump(Type = LumpType.LUMP_FACES)]
        private Face[] Faces { get; set; }

        [Lump(Type = LumpType.LUMP_VERTNORMALS)]
        private Vector[] VertNormals { get; set; }

        [Lump(Type = LumpType.LUMP_VERTNORMALINDICES)]
        private ushort[] VertNormalIndices { get; set; }

        [Lump(Type = LumpType.LUMP_PRIMITIVES)]
        private Primitive[] Primitives { get; set; }
        
        [Lump(Type = LumpType.LUMP_PRIMINDICES)]
        private ushort[] PrimitiveIndices { get; set; }

        [Lump(Type = LumpType.LUMP_LIGHTING)]
        private LightmapSample[] LightmapSamples { get; set; }

        [Lump(Type = LumpType.LUMP_LIGHTING_HDR)]
        private LightmapSample[] LightmapSamplesHdr { get; set; }
        
        [Lump(Type = LumpType.LUMP_FACES_HDR)]
        private Face[] FacesHdr { get; set; }

        private int _lightmapSize = -1;
        
        private int GetLightmapSize()
        {
            if (_lightmapSize != -1) return _lightmapSize;

            var maxPos = 1;
            foreach (var face in FacesHdr)
            {
                if (face.LightOffset == -1) continue;
                var max = Math.Max(face.LightMapOffsetX + face.LightMapSizeX, face.LightMapOffsetY + face.LightMapSizeY);
                maxPos = Math.Max(max, maxPos);
            }

            return _lightmapSize = Mathf.NextPowerOfTwo(maxPos);
        }

        public Texture2D GenerateLightmap()
        {
            var size = GetLightmapSize();
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);

            foreach (var face in FacesHdr)
            {
                if (face.LightOffset == -1) continue;

                var samplesWidth = face.LightMapSizeX + 1;
                var samplesHeight = face.LightMapSizeY + 1;

                for (var x = 0; x < samplesWidth; ++x)
                for (var y = 0; y < samplesHeight; ++y)
                {
                    var index = (face.LightOffset >> 2) + x + y*samplesWidth;

                    var sample = LightmapSamplesHdr[index];

                    texture.SetPixel(face.LightMapOffsetX + x, face.LightMapOffsetY + y, sample);
                }
            }

            texture.Apply();
            File.WriteAllBytes("lightmap.png", texture.EncodeToPNG());
            
            return texture;
        }
        
        public Mesh GenerateMesh()
        {
            var mesh = new Mesh();
            var meshGen = new MeshBuilder();

            var usedEdges = new HashSet<int>();
            var primitiveIndices = new List<int>();

            foreach (var face in FacesHdr)
            {
                var plane = Planes[face.PlaneNum];
                var normal = plane.Normal;

                meshGen.StartFace();

                for (var surfId = face.FirstEdge; surfId < face.FirstEdge + face.NumEdges; ++surfId)
                {
                    var surfEdge = SurfEdges[surfId];
                    var edgeIndex = Math.Abs(surfEdge);
                    usedEdges.Add(edgeIndex);
                    var edge = Edges[edgeIndex];
                    var vert = Vertices[surfEdge >= 0 ? edge.A : edge.B];

                    meshGen.AddVertex(vert, normal);
                }

                if (face.NumPrimitives == 0)
                {
                    meshGen.AddPrimitive(PrimitiveType.TriangleStrip);
                    meshGen.EndFace();
                    continue;
                }

                for (var primId = face.FirstPrimitive; primId < face.FirstPrimitive + face.NumPrimitives; ++primId)
                {
                    var primitive = Primitives[primId];
                    for (var indexId = primitive.FirstIndex;
                        indexId < primitive.FirstIndex + primitive.IndexCount;
                        ++indexId)
                    {
                        primitiveIndices.Add(PrimitiveIndices[indexId]);
                    }

                    meshGen.AddPrimitive(primitive.Type, primitiveIndices);

                    primitiveIndices.Clear();
                }

                meshGen.EndFace();
            }

            meshGen.CopyToMesh(mesh);

            return mesh;
        }
    }
}
