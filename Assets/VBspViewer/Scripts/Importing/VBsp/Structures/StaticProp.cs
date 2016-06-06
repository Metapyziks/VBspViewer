using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StaticPropV10
    {
        public Vector Origin;
        public Vector Angles;
        public ushort PropType;
        public ushort FirstLeaf;
        public ushort LeafCount;
        [MarshalAs(UnmanagedType.U1)] public bool Solid;
        public byte Flags;
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
