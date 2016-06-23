using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.Mdl
{
    public partial class MdlFile
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public int Id;
            public int Version;
            public int Checksum;

            private ulong _name0;
            private ulong _name1;
            private ulong _name2;
            private ulong _name3;
            private ulong _name4;
            private ulong _name5;
            private ulong _name6;
            private ulong _name7;

            public int Length;

            public Vector EyePosition;
            public Vector IllumPosition;
            public Vector HullMin;
            public Vector HullMax;
            public Vector ViewBbMin;
            public Vector ViewBbMax;

            public int Flags;

            public int NumBones;
            public int BoneIndex;

            public int NumBoneControllers;
            public int BoneControllerIndex;

            public int NumHitBoxSets;
            public int HitBoxSetIndex;

            public int NumLocalAnim;
            public int LocalAnimIndex;

            public int NumLocalSeq;
            public int LocalSeqIndex;

            public int ActivityListVersion;
            public int EventsIndexed;

            public int NumTextures;
            public int TextureIndex;

            public int NumCdTextures;
            public int CdTextureIndex;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StudioTexture
        {
            public int NameIndex;
            public int Flags;
            public int Used;

            private int _unused0;

            public int MaterialPtr;
            public int ClientMaterialPtr;

            public int _unused1;
            public int _unused2;
            public int _unused3;
            public int _unused4;
            public int _unused5;
            public int _unused6;
            public int _unused7;
            public int _unused8;
            public int _unused9;
            public int _unused10;
        }

        [ThreadStatic]
        private static StringBuilder _sBuilder;
        private static string ReadNullTerminatedString(Stream stream)
        {
            if (_sBuilder == null) _sBuilder = new StringBuilder();
            else _sBuilder.Remove(0, _sBuilder.Length);

            while (true)
            {
                var c = (char) stream.ReadByte();
                if (c == 0) return _sBuilder.ToString();
                _sBuilder.Append(c);
            }
        }

        private readonly IResourceProvider _loader;
        private readonly string _baseFilename;
        private readonly Header _header;

        private readonly StudioTexture[] _materials;
        private readonly string[] _materialNames;

        private readonly Dictionary<int, Mesh> _lods = new Dictionary<int, Mesh>(); 

        public int LodCount
        {
            get { return 1; }
        }

        public MdlFile(IResourceProvider loader, string filename)
        {
            _loader = loader;
            _baseFilename = filename.Substring(0, filename.Length - ".mdl".Length);

            using (var stream = loader.OpenFile(filename))
            {
                _header = ReadLumpWrapper<Header>.ReadSingleFromStream(stream);

                _materials = new StudioTexture[_header.NumTextures];
                _materialNames = new string[_header.NumTextures];

                stream.Seek(_header.TextureIndex, SeekOrigin.Begin);

                var index = 0;
                ReadLumpWrapper<StudioTexture>.ReadLumpFromStream(stream, _header.NumTextures, tex =>
                {
                    _materials[index] = tex;

                    stream.Seek(tex.NameIndex, SeekOrigin.Current);
                    _materialNames[index] = ReadNullTerminatedString(stream) + ".vmt";
                    ++index;
                });
            }
        }

        [ThreadStatic] private static List<Vector3> _sVertices;
        [ThreadStatic] private static List<Vector3> _sNormals;
        [ThreadStatic] private static List<Vector2> _sTexCoords;

        public string GetMaterialName(int lodLevel, int index)
        {
            // TODO: Material replacements

            return _materialNames[index];
        }

        private Mesh GetBaseMesh(int lodLevel)
        {
            Mesh mesh;
            if (_lods.TryGetValue(lodLevel, out mesh)) return mesh;

            mesh = new Mesh();

            using (Profiler.Begin("BuildPropMesh"))
            {
                var verts = GetVertices(lodLevel);
                var indices = GetTriangles(lodLevel);

                if (_sVertices == null) _sVertices = new List<Vector3>();
                else _sVertices.Clear();

                if (_sNormals == null) _sNormals = new List<Vector3>();
                else _sNormals.Clear();

                if (_sTexCoords == null) _sTexCoords = new List<Vector2>();
                else _sTexCoords.Clear();

                var transform = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(-90f, Vector3.up), Vector3.one);

                for (var i = 0; i < verts.Length; ++i)
                {
                    var vert = verts[i];

                    _sVertices.Add(transform * ((Vector3) vert.Position * VBspFile.SourceToUnityUnits));
                    _sNormals.Add(transform * (Vector3) vert.Normal);
                    _sTexCoords.Add(new Vector2(vert.TexCoordX, vert.TexCoordY));
                }

                mesh.SetVertices(_sVertices);
                mesh.SetNormals(_sNormals);
                mesh.SetUVs(0, _sTexCoords);

                mesh.subMeshCount = indices.Length;

                for (var i = 0; i < indices.Length; ++i)
                {
                    mesh.SetIndices(indices[i], MeshTopology.Triangles, i);
                }

                _lods.Add(lodLevel, mesh);
                return mesh;
            }
        }

        public Mesh GetMesh(int lodLevel, string vertexLightingFile)
        {
            var mesh = GetBaseMesh(lodLevel);
            if (string.IsNullOrEmpty(vertexLightingFile)) return mesh;

            mesh = UnityEngine.Object.Instantiate(mesh);
            mesh.colors = GetVertexLighting(vertexLightingFile, lodLevel);

            return mesh;
        }
    }
}
