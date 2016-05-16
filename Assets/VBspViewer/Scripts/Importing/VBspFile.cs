using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions.Must;
using Debug = System.Diagnostics.Debug;

namespace VBspViewer.Importing
{
    public class VBspFile
    {
        [MeansImplicitUse, AttributeUsage(AttributeTargets.Property)]
        private class LumpAttribute : Attribute
        {
            public LumpType Type { get; set; }
        }
        
        private delegate void ReadLumpDelegate(VBspFile file, LumpInfo info, BinaryReader reader);

        private class ReadLumpWrapper<T>
            where T : struct
        {
            [UsedImplicitly]
            private static T[] ReadLump(LumpInfo info, BinaryReader reader, Func<BinaryReader, T> readFunc)
            {
                var size = Marshal.SizeOf(typeof(T));
                var count = info.Length/size;
                var array = new T[count];
                for (var i = 0; i < count; ++i)
                {
                    array[i] = readFunc(reader);
                }

                return array;
            }
        }
        
        private static readonly Dictionary<Type, Expression> _sReadMethods = new Dictionary<Type, Expression>(); 
        private static Expression FindReadMethod(Type type, ParameterExpression readerParam)
        {
            Expression readMethod;
            if (_sReadMethods.TryGetValue(type, out readMethod)) return readMethod;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.Public;
            var method = type.GetMethod("Read", bFlags, null, new[] {typeof (BinaryReader)}, null);
            if (method != null)
            {
                readMethod = Expression.Call(method, readerParam);
                return CreateReadMethod(type, readerParam, readMethod);
            }

            method = typeof (BinaryReader).GetMethod("Read" + type.Name);
            if (method != null)
            {
                readMethod = Expression.Call(readerParam, method);
                return CreateReadMethod(type, readerParam, readMethod);
            }

            throw new NotImplementedException();
        }

        private static Expression CreateReadMethod(Type type, ParameterExpression readerParam, Expression call)
        {
            var delegType = typeof (Func<,>).MakeGenericType(typeof (BinaryReader), type);
            var lambda = Expression.Lambda(delegType, call, readerParam);
            var compiled = lambda.Compile();
            var constExpr = Expression.Constant(compiled, delegType);
            _sReadMethods.Add(type, constExpr);
            return constExpr;
        }

        private static readonly Dictionary<Type, MethodInfo> _sReadLumpMethods = new Dictionary<Type, MethodInfo>();
        private static MethodInfo FindReadLumpMethod(Type type)
        {
            MethodInfo readLumpMethod;
            if (_sReadLumpMethods.TryGetValue(type, out readLumpMethod)) return readLumpMethod;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.NonPublic;

            var readLumpWrapper = typeof (ReadLumpWrapper<>).MakeGenericType(type);
            var readLump = readLumpWrapper.GetMethod("ReadLump", bFlags);

            _sReadLumpMethods.Add(type, readLump);
            return readLump;
        } 
        
        private static Dictionary<LumpType, ReadLumpDelegate> _sReadLumpDelegates;
        private static Dictionary<LumpType, ReadLumpDelegate> GetReadLumpDelegates()
        {
            if (_sReadLumpDelegates != null) return _sReadLumpDelegates;

            _sReadLumpDelegates = new Dictionary<LumpType, ReadLumpDelegate>();

            var fileParam = Expression.Parameter(typeof (VBspFile), "file");
            var infoParam = Expression.Parameter(typeof (LumpInfo), "info");
            var readerParam = Expression.Parameter(typeof (BinaryReader), "reader");
            
            const BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var prop in typeof(VBspFile).GetProperties(bFlags))
            {
                var attrib = (LumpAttribute) prop.GetCustomAttributes(typeof(LumpAttribute), true).FirstOrDefault();
                if (attrib == null) continue;

                var type = prop.PropertyType.GetElementType();
                var readMethod = FindReadMethod(type, readerParam);
                var readLumpMethod = FindReadLumpMethod(type);

                var call = Expression.Call(readLumpMethod, infoParam, readerParam, readMethod);
                var set = Expression.Call(fileParam, prop.GetSetMethod(true), call);
                var lambda = Expression.Lambda<ReadLumpDelegate>(set, fileParam, infoParam, readerParam);

                _sReadLumpDelegates.Add(attrib.Type, lambda.Compile());
            }

            return _sReadLumpDelegates;
        }
        
        private class Header
        {
            public const int LumpInfoCount = 64;

            public static Header Read(BinaryReader reader)
            {
                var header = new Header
                {
                    Identifier = reader.ReadInt32(),
                    Version = reader.ReadInt32()
                };

                var lumps = new LumpInfo[LumpInfoCount];
                for (var i = 0; i < LumpInfoCount; ++i)
                {
                    lumps[i] = LumpInfo.Read(reader);
                }

                header.Lumps = lumps;
                header.MapRevision = reader.ReadInt32();

                return header;
            }

            public int Identifier;
            public int Version;
            public LumpInfo[] Lumps;
            public int MapRevision;
        }

        private enum LumpType
        {
            LUMP_ENTITIES,
            LUMP_PLANES,
            LUMP_TEXDATA,
            LUMP_VERTEXES,
            LUMP_VISIBILITY,
            LUMP_NODES,
            LUMP_TEXINFO,
            LUMP_FACES,
            LUMP_LIGHTING,
            LUMP_OCCLUSION,
            LUMP_LEAFS,
            LUMP_FACEIDS,
            LUMP_EDGES,
            LUMP_SURFEDGES,
            LUMP_MODELS,
            LUMP_WORLDLIGHTS,
            LUMP_LEAFFACES,
            LUMP_LEAFBRUSHES,
            LUMP_BRUSHES,
            LUMP_BRUSHSIDES,
            LUMP_AREAS,
            LUMP_AREAPORTALS,
            LUMP_PORTALS,
            LUMP_CLUSTERS,
            LUMP_PORTALVERTS,
            LUMP_CLUSTERPORTALS,
            LUMP_DISPINFO,
            LUMP_ORIGINALFACES,
            LUMP_PHYSDISP,
            LUMP_PHYSCOLLIDE,
            LUMP_VERTNORMALS,
            LUMP_VERTNORMALINDICES,
            LUMP_DISP_LIGHTMAP_ALPHAS,
            LUMP_DISP_VERTS,
            LUMP_DISP_LIGHTMAP_SAMPLE_POSITIONS,
            LUMP_GAME_LUMP,
            LUMP_LEAFWATERDATA,
            LUMP_PRIMITIVES,
            LUMP_PRIMVERTS,
            LUMP_PRIMINDICES,
            LUMP_PAKFILE,
            LUMP_CLIPPORTALVERTS,
            LUMP_CUBEMAPS,
            LUMP_TEXDATA_STRING_DATA,
            LUMP_TEXDATA_STRING_TABLE,
            LUMP_OVERLAYS,
            LUMP_LEAFMINDISTTOWATER,
            LUMP_FACE_MACRO_TEXTURE_INFO,
            LUMP_DISP_TRIS,
            LUMP_PHYSCOLLIDESURFACE,
            LUMP_WATEROVERLAYS,
            LUMP_LIGHTMAPPAGES,
            LUMP_LIGHTMAPPAGEINFOS,
            LUMP_LIGHTING_HDR,
            LUMP_WORLDLIGHTS_HDR,
            LUMP_LEAF_AMBIENT_LIGHTING_HDR,
            LUMP_LEAF_AMBIENT_LIGHTING,
            LUMP_XZIPPAKFILE,
            LUMP_FACES_HDR,
            LUMP_MAP_FLAGS,
            LUMP_OVERLAY_FADES,
            LUMP_OVERLAY_SYSTEM_LEVELS,
            LUMP_PHYSLEVEL,
            LUMP_DISP_MULTIBLEND
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LumpInfo
        {
            public static LumpInfo Read(BinaryReader reader)
            {
                return new LumpInfo
                {
                    Offset = reader.ReadInt32(),
                    Length = reader.ReadInt32(),
                    Version = reader.ReadInt32(),
                    IdentCode = reader.ReadInt32()
                };
            }

            public int Offset;
            public int Length;
            public int Version;
            public int IdentCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Vector : IEquatable<Vector>
        {
            public static Vector Read(BinaryReader reader)
            {
                return new Vector
                {
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Z = reader.ReadSingle()
                };
            }
            
            public static implicit operator Vector3(Vector vector)
            {
                return new Vector3(vector.X, vector.Z, vector.Y);
            }

            public float X;
            public float Y;
            public float Z;

            public Vector(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }
            
            public bool Equals(Vector other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is Vector && Equals((Vector) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = X.GetHashCode();
                    hashCode = (hashCode*397) ^ Y.GetHashCode();
                    hashCode = (hashCode*397) ^ Z.GetHashCode();
                    return hashCode;
                }
            }

            public override string ToString()
            {
                return string.Format("{0:F2}, {1:F2}, {2:F2}", X, Y, Z);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Plane
        {
            public static Plane Read(BinaryReader reader)
            {
                return new Plane
                {
                    Normal = Vector.Read(reader),
                    Distance = reader.ReadSingle(),
                    Type = reader.ReadInt32()
                };
            }

            public Vector Normal;
            public float Distance;
            public int Type;
        }

        private enum PrimitiveType : ushort
        {
            TriangleList,
            TriangleStrip
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Primitive
        {
            public static Primitive Read(BinaryReader reader)
            {
                return new Primitive
                {
                    Type = (PrimitiveType) reader.ReadUInt16(),
                    FirstIndex = reader.ReadUInt16(),
                    IndexCount = reader.ReadUInt16(),
                    FirstVert = reader.ReadUInt16(),
                    VertCount = reader.ReadUInt16()
                };
            }

            public PrimitiveType Type;
            public ushort FirstIndex;
            public ushort IndexCount;
            public ushort FirstVert;
            public ushort VertCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Edge
        {
            public static Edge Read(BinaryReader reader)
            {
                return new Edge
                {
                    A = reader.ReadUInt16(),
                    B = reader.ReadUInt16()
                };
            }

            public ushort A;
            public ushort B;
        }

        private enum Side : byte
        {
            OutFacing = 0,
            InFacing = 1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Face
        {
            public static Face Read(BinaryReader reader)
            {
                return new Face
                {
                    PlaneNum = reader.ReadUInt16(),
                    Side = (Side) reader.ReadByte(),
                    OnNode = reader.ReadBoolean(),
                    FirstEdge = reader.ReadInt32(),
                    NumEdges = reader.ReadInt16(),
                    TexInfo = reader.ReadInt16(),
                    DispInfo = reader.ReadInt16(),
                    FogVolumeId = reader.ReadInt16(),
                    Styles = reader.ReadInt32(),
                    LightOffset = reader.ReadInt32(),
                    Area = reader.ReadSingle(),
                    LightMapOffsetX = reader.ReadInt32(),
                    LightMapOffsetY = reader.ReadInt32(),
                    LightMapSizeX = reader.ReadInt32(),
                    LightMapSizeY = reader.ReadInt32(),
                    OriginalFace = reader.ReadInt32(),
                    NumPrimitives = reader.ReadUInt16(),
                    FirstPrimitive = reader.ReadUInt16(),
                    SmoothingGroups = reader.ReadUInt32()
                };
            }

            public ushort PlaneNum;
            public Side Side;
            public bool OnNode;
            public int FirstEdge;
            public short NumEdges;
            public short TexInfo;
            public short DispInfo;
            public short FogVolumeId;
            public int Styles;
            public int LightOffset;
            public float Area;
            public int LightMapOffsetX;
            public int LightMapOffsetY;
            public int LightMapSizeX;
            public int LightMapSizeY;
            public int OriginalFace;
            public ushort NumPrimitives;
            public ushort FirstPrimitive;
            public uint SmoothingGroups;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct LightmapSample
        {
            public static LightmapSample Read(BinaryReader reader)
            {
                return new LightmapSample
                {
                    R = reader.ReadByte(),
                    G = reader.ReadByte(),
                    B = reader.ReadByte(),
                    Exponent = reader.ReadSByte()
                };
            }

            public static implicit operator Color(LightmapSample sample)
            {
                return new Color32((byte) (sample.R << sample.Exponent),
                    (byte) (sample.G << sample.Exponent),
                    (byte) (sample.B << sample.Exponent), 255);
            }

            public byte R;
            public byte G;
            public byte B;
            public sbyte Exponent;
        }

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
                    
                    UnityEngine.Debug.LogFormat("{0} Start: 0x{1:x}, Length: 0x{2:x}", (LumpType) lumpIndex, info.Offset, info.Length);

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

        [Lump(Type = LumpType.LUMP_LIGHTING_HDR)]
        private LightmapSample[] LightmapSamples { get; set; }

        private int _lightmapSize = -1;
        
        private readonly List<Edge> _usedEdges = new List<Edge>();
        private readonly List<Edge> _unusedEdges = new List<Edge>();

        public void DebugRenderEdges(float duration = 0)
        {
            const float inchesToMetre = 1f/39.3701f;

            foreach (var edge in _unusedEdges)
            {
                UnityEngine.Debug.DrawLine((Vector3) Vertices[edge.A] * inchesToMetre, (Vector3) Vertices[edge.B] * inchesToMetre, Color.red, duration);
            }

            foreach (var edge in _usedEdges)
            {
                UnityEngine.Debug.DrawLine((Vector3) Vertices[edge.A] * inchesToMetre, (Vector3) Vertices[edge.B] * inchesToMetre, Color.green, duration);
            }
        }

        private int GetLightmapSize()
        {
            if (_lightmapSize != -1) return _lightmapSize;

            var maxPos = 1;
            foreach (var face in Faces)
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

            var writer = File.CreateText("lightmap.txt");

            foreach (var face in Faces)
            {
                if (face.LightOffset == -1) continue;

                var samplesWidth = face.LightMapSizeX + 1;
                var samplesHeight = face.LightMapSizeY + 1;

                for (var x = 0; x < samplesWidth; ++x)
                for (var y = 0; y < samplesHeight; ++y)
                {
                    var index = face.LightOffset + x + y*samplesWidth;
                    var sample = LightmapSamples[index];

                    writer.WriteLine(sample.Exponent);
                        
                    texture.SetPixel(face.LightMapOffsetX + x, face.LightMapOffsetY + y, sample);
                }
            }

            writer.Dispose();

            texture.Apply();
            File.WriteAllBytes("lightmap.png", texture.EncodeToPNG());
            
            return texture;
        }

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

        private class MeshBuilder
        {
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

        public Mesh GenerateMesh()
        {
            var mesh = new Mesh();
            var meshGen = new MeshBuilder();

            var usedEdges = new HashSet<int>();
            var primitiveIndices = new List<int>();

            foreach (var face in Faces)
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

            _unusedEdges.Clear();

            for (var i = 0; i < Edges.Length; ++i)
            {
                var used = usedEdges.Contains(i);
                (used ? _usedEdges : _unusedEdges).Add(Edges[i]);
            }
            
            return mesh;
        }
    }
}
