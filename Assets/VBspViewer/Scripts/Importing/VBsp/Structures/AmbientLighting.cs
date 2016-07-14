using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AmbientLighting
    {
        public CompressedLightCube Cube;
        public byte X;
        public byte Y;
        public byte Z;
        private byte _padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct AmbientIndex
    {
        public ushort SampleCount;
        public ushort FirstSample;
    }
}
