using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
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

        private Mesh GetMesh(int lod)
        {
            Mesh mesh;
            if (_lods.TryGetValue(lod, out mesh)) return mesh;

            mesh = new Mesh();



            _lods.Add(lod, mesh);
            return mesh;
        }
    }
}
