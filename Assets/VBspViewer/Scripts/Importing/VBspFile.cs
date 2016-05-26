using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using UnityEngine;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Structures;
using Plane = VBspViewer.Importing.Structures.Plane;
using PrimitiveType = VBspViewer.Importing.Structures.PrimitiveType;

namespace VBspViewer.Importing
{
    public partial class VBspFile
    {
        public const float SourceToUnityUnits = 0.01905f;

        private readonly Header _header;
        
        public VBspFile(Stream stream)
        {
            var reader = new BinaryReader(stream);
            _header = Header.Read(reader);

            var delegates = GetReadLumpDelegates();
            var srcBuffer = new byte[0];
            
            for (var lumpIndex = 0; lumpIndex < Header.LumpInfoCount; ++lumpIndex)
            {
                ReadLumpDelegate deleg;
                if (!delegates.TryGetValue((LumpType) lumpIndex, out deleg)) continue;

                var info = _header.Lumps[lumpIndex];
                    
                Debug.LogFormat("{0} Start: 0x{1:x}, Length: 0x{2:x}", (LumpType) lumpIndex, info.Offset, info.Length);

                if (srcBuffer.Length < info.Length)
                {
                    srcBuffer = new byte[Mathf.NextPowerOfTwo(info.Length)];
                }

                stream.Seek(info.Offset, SeekOrigin.Begin);
                var read = stream.Read(srcBuffer, 0, info.Length);

                Debug.Assert(read == info.Length);

                deleg(this, srcBuffer, info.Length);
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

        [Lump(Type = LumpType.LUMP_TEXINFO)]
        private TextureInfo[] TexInfos { get; set; }

        [Lump(Type = LumpType.LUMP_DISPINFO)]
        private DispInfo[] DispInfos { get; set; }

        [Lump(Type = LumpType.LUMP_DISP_VERTS)]
        private DispVert[] DispVerts { get; set; }

        [Lump(Type = LumpType.LUMP_DISP_LIGHTMAP_SAMPLE_POSITIONS)]
        private int[] DispLightmapSampleIndices { get; set; }

        [Lump(Type = LumpType.LUMP_ENTITIES)]
        private byte[] Entities { get; set; }
        
        private readonly Dictionary<int, Rect> _lightmapRects = new Dictionary<int, Rect>(); 

        public Texture2D GenerateLightmap()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            var litFaces = FacesHdr.Count(x => x.LightOffset != -1);
            var textures = new Texture2D[litFaces];

            var texIndex = 0;
            foreach (var face in FacesHdr)
            {
                if (face.LightOffset == -1) continue;

                var samplesWidth = face.LightMapSizeX + 1;
                var samplesHeight = face.LightMapSizeY + 1;

                var subTex = new Texture2D(samplesWidth, samplesHeight, TextureFormat.RGB24, false);
                
                for (var x = 0; x < samplesWidth; ++x)
                for (var y = 0; y < samplesHeight; ++y)
                {
                    var index = (face.LightOffset >> 2) + x + y*samplesWidth;
                    var sample = LightmapSamplesHdr[index];

                    subTex.SetPixel(x, y, sample);
                }

                textures[texIndex++] = subTex;
            }

            _lightmapRects.Clear();
            var rects = texture.PackTextures(textures, 0);

            texIndex = 0;
            for (var faceIndex = 0; faceIndex < FacesHdr.Length; ++faceIndex)
            {
                var face = FacesHdr[faceIndex];
                if (face.LightOffset == -1) continue;

                var rect = rects[texIndex++];
                _lightmapRects.Add(faceIndex, rect);
            }

            texture.Apply();
            return texture;
        }

        private static Vector2 GetUv(Vector3 pos, TexAxis uAxis, TexAxis vAxis)
        {
            return new Vector2(
                Vector3.Dot(pos, uAxis.Normal) + uAxis.Offset,
                Vector3.Dot(pos, vAxis.Normal) + vAxis.Offset);
        }

        private static Vector2 GetLightmapUv(Vector3 pos, Face face, TextureInfo texInfo, Rect lightmapRect)
        {
            var lightmapUv = GetUv(pos, texInfo.LightmapUAxis, texInfo.LightmapVAxis);

            lightmapUv.x -= face.LightMapOffsetX - .5f;
            lightmapUv.y -= face.LightMapOffsetY - .5f;
            lightmapUv.x /= face.LightMapSizeX + 1f;
            lightmapUv.y /= face.LightMapSizeY + 1f;
            
            lightmapUv.x *= lightmapRect.width;
            lightmapUv.y *= lightmapRect.height;
            lightmapUv.x += lightmapRect.x;
            lightmapUv.y += lightmapRect.y;

            return lightmapUv;
        }

        private Vector3 GetDisplacementVertex(int offset, int x, int y, int size, Vector3[] corners)
        {
            var vert = DispVerts[offset + x + y*(size + 1)];

            var tx = (float) x/size;
            var ty = (float) y/size;
            var sx = 1f - tx;
            var sy = 1f - ty;

            var origin = ty*(sx*corners[1] + tx*corners[2]) + sy*(sx*corners[0] + tx*corners[3]);

            return origin + (Vector3) vert.Vector*vert.Distance;
        }

        private Mesh GenerateMesh(MeshBuilder meshGen, IEnumerable<int> faceIndices)
        {
            var mesh = new Mesh();
            var primitiveIndices = new List<int>();

            const SurfFlags ignoreFlags = SurfFlags.NODRAW | SurfFlags.SKIP | SurfFlags.SKY | SurfFlags.SKY2D | SurfFlags.HINT | SurfFlags.TRIGGER;

            var corners = new Vector3[4];

            foreach (var faceIndex in faceIndices)
            {
                var face = FacesHdr[faceIndex];
                var plane = Planes[face.PlaneNum];
                var tex = TexInfos[face.TexInfo];

                if ((tex.Flags & ignoreFlags) != 0) continue;

                Rect lightmapRect;
                if (!_lightmapRects.TryGetValue(faceIndex, out lightmapRect))
                {
                    lightmapRect = new Rect(0f, 0f, 1f, 1f);
                }

                if (face.DispInfo != -1)
                {
                    Debug.Assert(face.NumEdges == 4);

                    var disp = DispInfos[face.DispInfo];
                    var size = 1 << disp.Power;

                    for (var surfId = face.FirstEdge; surfId < face.FirstEdge + face.NumEdges; ++surfId)
                    {
                        var surfEdge = SurfEdges[surfId];
                        var edgeIndex = Math.Abs(surfEdge);
                        var edge = Edges[edgeIndex];
                        var vert = Vertices[surfEdge >= 0 ? edge.A : edge.B];

                        corners[surfId - face.FirstEdge] = (Vector3) vert;
                    }

                    lightmapRect = new Rect(lightmapRect.x, lightmapRect.yMax, lightmapRect.width, -lightmapRect.height);

                    for (var x = 0; x < size; ++x)
                    for (var y = 0; y < size; ++y)
                    {
                        var a = GetDisplacementVertex(disp.DispVertStart, x, y,         size, corners);
                        var b = GetDisplacementVertex(disp.DispVertStart, x + 1, y,     size, corners);
                        var c = GetDisplacementVertex(disp.DispVertStart, x + 1, y + 1, size, corners);
                        var d = GetDisplacementVertex(disp.DispVertStart, x, y + 1,     size, corners);
                        
                        meshGen.StartFace();
                        meshGen.AddVertex(b * SourceToUnityUnits, Vector3.up, GetLightmapUv(b, face, tex, lightmapRect));
                        meshGen.AddVertex(a * SourceToUnityUnits, Vector3.up, GetLightmapUv(a, face, tex, lightmapRect));
                        meshGen.AddVertex(d * SourceToUnityUnits, Vector3.up, GetLightmapUv(d, face, tex, lightmapRect));
                        meshGen.AddVertex(c * SourceToUnityUnits, Vector3.up, GetLightmapUv(c, face, tex, lightmapRect));
                        meshGen.AddPrimitive(PrimitiveType.TriangleStrip);
                        meshGen.EndFace();
                    }

                    continue;
                }

                for (var surfId = face.FirstEdge; surfId < face.FirstEdge + face.NumEdges; ++surfId)
                {
                    var surfEdge = SurfEdges[surfId];
                    var edgeIndex = Math.Abs(surfEdge);
                    var edge = Edges[edgeIndex];
                    var vert = Vertices[surfEdge >= 0 ? edge.A : edge.B];
                    var lightmapUv = GetLightmapUv(vert, face, tex, lightmapRect);

                    meshGen.AddVertex((Vector3) vert * SourceToUnityUnits, plane.Normal, lightmapUv);
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
        
        public Mesh[] GenerateMeshes()
        {
            var meshGen = new MeshBuilder();
            const int facesPerMesh = 1024;

            var meshCount = (FacesHdr.Length + facesPerMesh - 1)/facesPerMesh;
            var meshes = new Mesh[meshCount];

            for (var meshIndex = 0; meshIndex < meshCount; ++meshIndex)
            {
                var firstFace = meshIndex * facesPerMesh;
                var endFace = Math.Min(firstFace+facesPerMesh, FacesHdr.Length);

                meshGen.Clear();
                meshes[meshIndex] = GenerateMesh(meshGen, Enumerable.Range(firstFace, endFace - firstFace));
            }

            return meshes;
        }

        [ThreadStatic]
        private static StringBuilder _sDecodeBuilder;
        private static string DecodeEscapedString(string str)
        {
            if (_sDecodeBuilder == null) _sDecodeBuilder = new StringBuilder();
            else _sDecodeBuilder.Remove(0, _sDecodeBuilder.Length);

            for (var i = 0; i < str.Length; ++i)
            {
                if (str[i] != '\\') _sDecodeBuilder.Append(str[i]);
            }

            return _sDecodeBuilder.ToString();
        }

        private static IEnumerable<KeyValuePair<string, EntValue>> GetEntityKeyVals(string entMatch, Regex keyValRegex)
        {
            foreach (var pairMatch in keyValRegex.Matches(entMatch).Cast<Match>())
            {
                var key = DecodeEscapedString(pairMatch.Groups["key"].Value);
                var value = DecodeEscapedString(pairMatch.Groups["value"].Value);
                yield return new KeyValuePair<string, EntValue>(key, EntValue.Parse(value));
            }
        }

        public IEnumerable<IEnumerable<KeyValuePair<string, EntValue>>> GetEntityKeyVals()
        {
            var text = Encoding.ASCII.GetString(Entities);
            const string stringPattern = @"""(?<{0}>([^\\""]|\\.)*)""";
            var keyValuePattern = string.Format(stringPattern, "key") + @"\s*" + string.Format(stringPattern, "value");
            var entityPattern = @"{(?<entity>\s*(" + keyValuePattern + @"\s*)*)}";

            var entityRegex = new Regex(entityPattern);
            var keyValueRegex = new Regex(keyValuePattern);

            foreach (var entMatch in entityRegex.Matches(text).Cast<Match>())
            {
                yield return GetEntityKeyVals(entMatch.Value, keyValueRegex);
            }
        }
    }
}
