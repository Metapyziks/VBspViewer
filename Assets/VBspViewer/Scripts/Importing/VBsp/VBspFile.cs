using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Assets.VBspViewer.Scripts.Importing.VBsp;
using UnityEngine;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.VBsp.Structures;
using Plane = VBspViewer.Importing.VBsp.Structures.Plane;
using PrimitiveType = VBspViewer.Importing.VBsp.Structures.PrimitiveType;

namespace VBspViewer.Importing.VBsp
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
            
            using (Profiler.Begin("ReadLumps"))
            {
                for (var lumpIndex = 0; lumpIndex < Header.LumpInfoCount; ++lumpIndex)
                {
                    var info = _header.Lumps[lumpIndex];

                    if ((LumpType) lumpIndex == LumpType.LUMP_GAME_LUMP)
                    {
                        Debug.LogFormat("{0} Start: 0x{1:x}, Length: 0x{2:x}", LumpType.LUMP_GAME_LUMP, info.Offset, info.Length);

                        using (Profiler.Begin("ReadLump({0})", LumpType.LUMP_GAME_LUMP))
                        {
                            stream.Seek(info.Offset, SeekOrigin.Begin);
                            var lumpCount = reader.ReadInt32();
                            GameLumps = new GameLumpInfo[lumpCount];
                            for (var i = 0; i < lumpCount; ++i) GameLumps[i] = new GameLumpInfo(reader);
                            for (var i = 0; i < lumpCount; ++i) GameLumps[i].ReadContents(stream);
                        }
                        continue;
                    }

                    ReadLumpDelegate deleg;
                    if (!delegates.TryGetValue((LumpType) lumpIndex, out deleg)) continue;

                    Debug.LogFormat("{0} Start: 0x{1:x}, Length: 0x{2:x}", (LumpType) lumpIndex, info.Offset, info.Length);

                    using (Profiler.Begin("ReadLump({0})", (LumpType) lumpIndex))
                    {
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
            }

            PakFile = new PakFile(PakFileData);
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
        private byte[] LightmapSamplesHdr { get; set; }
        
        [Lump(Type = LumpType.LUMP_FACES_HDR)]
        private Face[] FacesHdr { get; set; }

        [Lump(Type = LumpType.LUMP_TEXINFO)]
        private TextureInfo[] TexInfos { get; set; }

        [Lump(Type = LumpType.LUMP_DISPINFO)]
        private DispInfo[] DispInfos { get; set; }

        [Lump(Type = LumpType.LUMP_DISP_VERTS)]
        private DispVert[] DispVerts { get; set; }

        [Lump(Type = LumpType.LUMP_ENTITIES)]
        private byte[] Entities { get; set; }

        [Lump(Type = LumpType.LUMP_PROPCOLLISION)]
        private short[] PropConvexHulls { get; set; }

        [Lump(Type = LumpType.LUMP_PROPHULLS)]
        private short[] PropHulls { get; set; }

        [Lump(Type = LumpType.LUMP_PROPHULLVERTS)]
        private Vector[] PropHullVertices { get; set; }

        [Lump(Type = LumpType.LUMP_PROPTRIS)]
        private short[] PropHullFaces { get; set; }

        [Lump(Type = LumpType.LUMP_PHYSCOLLIDE)]
        private byte[] PhysCollisionData { get; set; }

        [Lump(Type = LumpType.LUMP_PAKFILE)]
        private byte[] PakFileData { get; set; }

        [Lump(Type = LumpType.LUMP_MODELS)]
        private Model[] Models { get; set; }

        private GameLumpInfo[] GameLumps { get; set; }
        
        private readonly Dictionary<int, Rect> _lightmapRects = new Dictionary<int, Rect>();

        public PakFile PakFile { get; private set; }

        public Texture2D GenerateLightmap()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            var litFaces = FacesHdr.Count(x => x.LightOffset != -1);
            var textures = new Texture2D[litFaces];

            using (Profiler.Begin("ReadLightmapSubtextures"))
            {
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
                        var index = face.LightOffset + ((x + y*samplesWidth) << 2);

                        var r = LightmapSamplesHdr[index + 0];
                        var g = LightmapSamplesHdr[index + 1];
                        var b = LightmapSamplesHdr[index + 2];
                        var e = (sbyte) LightmapSamplesHdr[index + 3];

                        subTex.SetPixel(x, y, new LightmapSample {R = r, G = g, B = b, Exponent = e});
                    }

                    textures[texIndex++] = subTex;
                }
            }

            using (Profiler.Begin("GenerateLightmap"))
            {
                _lightmapRects.Clear();
                var rects = texture.PackTextures(textures, 0);

                var texIndex = 0;
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

        private static Vector2 GetLightmapUv(int x, int y, int size, Face face, Rect lightmapRect)
        {
            var lightmapUv = new Vector2((float) x / size, (float) y / size);
            var lightmapScale = new Vector2(1f/(face.LightMapSizeX + 1), 1f/(face.LightMapSizeY + 1));

            lightmapUv = Vector2.Scale(lightmapUv, new Vector2(face.LightMapSizeX, face.LightMapSizeY));
            lightmapUv += new Vector2(.5f, .5f);
            lightmapUv = Vector2.Scale(lightmapUv, lightmapScale);

            lightmapUv.x *= lightmapRect.width;
            lightmapUv.y *= lightmapRect.height;
            lightmapUv.x += lightmapRect.x;
            lightmapUv.y += lightmapRect.y;

            return lightmapUv;
        }

        private Vector3 GetDisplacementVertex(int offset, int x, int y, int size, Vector3[] corners, int firstCorner)
        {
            var vert = DispVerts[offset + x + y*(size + 1)];

            var tx = (float) x/size;
            var ty = (float) y/size;
            var sx = 1f - tx;
            var sy = 1f - ty;

            var cornerA = corners[(0 + firstCorner) & 3];
            var cornerB = corners[(1 + firstCorner) & 3];
            var cornerC = corners[(2 + firstCorner) & 3];
            var cornerD = corners[(3 + firstCorner) & 3];

            var origin = ty*(sx*cornerB + tx*cornerC) + sy*(sx*cornerA + tx*cornerD);

            return origin + (Vector3) vert.Vector*vert.Distance;
        }

        private Mesh GenerateMesh(MeshBuilder meshGen, IEnumerable<int> faceIndices)
        {
            var mesh = new Mesh();
            var primitiveIndices = new List<int>();

            const SurfFlags ignoreFlags = SurfFlags.NODRAW | SurfFlags.SKIP | SurfFlags.SKY | SurfFlags.SKY2D | SurfFlags.HINT | SurfFlags.TRIGGER;

            var corners = new Vector3[4];

            using (Profiler.Begin("GenerateMesh"))
            {
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
                        var firstCorner = 0;
                        var firstCornerDist2 = float.MaxValue;

                        for (var surfId = face.FirstEdge; surfId < face.FirstEdge + face.NumEdges; ++surfId)
                        {
                            var surfEdge = SurfEdges[surfId];
                            var edgeIndex = Math.Abs(surfEdge);
                            var edge = Edges[edgeIndex];
                            var vert = Vertices[surfEdge >= 0 ? edge.A : edge.B];

                            corners[surfId - face.FirstEdge] = vert;

                            var dist2 = ((Vector3) disp.StartPosition - vert).sqrMagnitude;
                            if (dist2 < firstCornerDist2)
                            {
                                firstCorner = surfId - face.FirstEdge;
                                firstCornerDist2 = dist2;
                            }
                        }

                        for (var x = 0; x < size; ++x)
                            for (var y = 0; y < size; ++y)
                            {
                                var a = GetDisplacementVertex(disp.DispVertStart, x, y,         size, corners, firstCorner);
                                var b = GetDisplacementVertex(disp.DispVertStart, x, y + 1,     size, corners, firstCorner);
                                var c = GetDisplacementVertex(disp.DispVertStart, x + 1, y + 1, size, corners, firstCorner);
                                var d = GetDisplacementVertex(disp.DispVertStart, x + 1, y,     size, corners, firstCorner);

                                meshGen.StartFace();
                                meshGen.AddVertex(a * SourceToUnityUnits, plane.Normal, GetLightmapUv(x, y, size, face, lightmapRect));
                                meshGen.AddVertex(b * SourceToUnityUnits, plane.Normal, GetLightmapUv(x, y + 1, size, face, lightmapRect));
                                meshGen.AddVertex(c * SourceToUnityUnits, plane.Normal, GetLightmapUv(x + 1, y + 1, size, face, lightmapRect));
                                meshGen.AddVertex(d * SourceToUnityUnits, plane.Normal, GetLightmapUv(x + 1, y, size, face, lightmapRect));
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

                    if (face.NumPrimitives == 0 || face.NumPrimitives >= 0x8000)
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
            }

            meshGen.CopyToMesh(mesh);

            return mesh;
        }
        
        public Mesh[] GenerateMeshes(int modelNum)
        {
            var meshGen = new MeshBuilder();
            const int facesPerMesh = 1024;
            
            var meshes = new List<Mesh>();
            var model = Models[modelNum];

            for (var faceOffset = 0; faceOffset < model.NumFaces; faceOffset += facesPerMesh)
            {
                var count = Math.Min(faceOffset + facesPerMesh, model.NumFaces) - faceOffset;

                meshGen.Clear();
                meshGen.Offset = model.Origin;
                meshes.Add(GenerateMesh(meshGen, Enumerable.Range(model.FirstFace + faceOffset, count)));
            }

            return meshes.ToArray();
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

        private static IEnumerable<KeyValuePair<string, EntValue>> GetStaticPropKeyVals(StaticPropV10 prop,
            string modelName, string vertexLighting)
        {
            yield return new KeyValuePair<string, EntValue>("classname", "prop_static");
            yield return new KeyValuePair<string, EntValue>("hammerid", -1);
            yield return new KeyValuePair<string, EntValue>("origin", prop.Origin);
            yield return new KeyValuePair<string, EntValue>("angles", prop.Angles);
            yield return new KeyValuePair<string, EntValue>("model", modelName);
            yield return new KeyValuePair<string, EntValue>("vlighting", vertexLighting);
            yield return new KeyValuePair<string, EntValue>("skin", prop.Skin);
            yield return new KeyValuePair<string, EntValue>("solid", prop.Solid);
            yield return new KeyValuePair<string, EntValue>("flags", (int) prop.Flag);
            yield return new KeyValuePair<string, EntValue>("unknown", string.Format("{0:x2}, {1:x4}, {2:x8}", prop.Unknown0, prop.Unknown1, prop.Unknown2));
        } 

        public IEnumerable<IEnumerable<KeyValuePair<string, EntValue>>> GetEntityKeyVals()
        {
            var text = Encoding.ASCII.GetString(Entities);
            File.WriteAllText("entities.txt", text);
            const string stringPattern = @"""(?<{0}>([^\\""]|\\.)*)""";
            var keyValuePattern = string.Format(stringPattern, "key") + @"\s*" + string.Format(stringPattern, "value");
            var entityPattern = @"{(?<entity>\s*(" + keyValuePattern + @"\s*)*)}";

            var entityRegex = new Regex(entityPattern);
            var keyValueRegex = new Regex(keyValuePattern);

            foreach (var entMatch in entityRegex.Matches(text).Cast<Match>())
            {
                yield return GetEntityKeyVals(entMatch.Value, keyValueRegex);
            }

            var propLump = GameLumps.FirstOrDefault(x => x.Id == 0x73707270);
            if (propLump == null) yield break;

            Debug.Assert(propLump.Version == 10);

            string[] modelNames;
            int propCount;
            int readOffset;

            using (var stream = new MemoryStream(propLump.Contents))
            using (var reader = new BinaryReader(stream))
            {
                var modelNameCount = reader.ReadInt32();
                modelNames = new string[modelNameCount];

                var nameBuffer = new byte[128];
                for (var i = 0; i < modelNameCount; ++i)
                {
                    stream.Read(nameBuffer, 0, 128);
                    modelNames[i] = Encoding.ASCII.GetString(nameBuffer).TrimEnd('\0');
                }

                var leafCount = reader.ReadInt32();
                stream.Seek(leafCount*2, SeekOrigin.Current);

                propCount = reader.ReadInt32();
                readOffset = (int) reader.BaseStream.Position;
            }

            var props = ReadLumpWrapper<StaticPropV10>.ReadLump(propLump.Contents, readOffset,
                propCount*Marshal.SizeOf(typeof (StaticPropV10)));

            var index = 0;
            foreach (var prop in props)
            {
                var modelName = modelNames[prop.PropType];
                var hdrLighting = string.Format("sp_hdr_{0}.vhv", index);
                var ldrLighting = string.Format("sp_{0}.vhv", index);

                var lightingFile = PakFile.ContainsFile(hdrLighting) ? hdrLighting
                    : PakFile.ContainsFile(ldrLighting) ? ldrLighting : null;

                yield return GetStaticPropKeyVals(prop, modelName, lightingFile);

                ++index;
            }
        }
    }
}
