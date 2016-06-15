using System;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [Flags]
    public enum StaticPropFlag : byte
    {
        Fades = 1,
        UseLightingOrigin = 2,
        NoDraw = 4,
        IgnoreNormals = 8,
        NoShadow = 0x10,
        Unused = 0x20,
        NoPerVertexLighting = 0x40,
        NoSelfShadowing = 0x80
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StaticPropV10
    {
        public Vector Origin;
        public Vector Angles;
        public ushort PropType;
        public ushort FirstLeaf;
        public ushort LeafCount;
        [MarshalAs(UnmanagedType.U1)] public bool Solid;
        public StaticPropFlag Flag;
        public int Skin;
        public float FadeMinDist;
        public float FadeMaxDist;
        public Vector LightingOrigin;

        public float ForcedFadeScale;

        public byte MinCpuLevel;
        public byte MaxCpuLevel;
        public byte MinGpuLevel;
        public byte MaxGpuLevel;

        public uint ColorModulation;
        [MarshalAs(UnmanagedType.U1)] public bool DisableX360;

        public byte Unknown0;
        public ushort Unknown1;
        public uint Unknown2;
    }
}
