using System;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [Flags]
    public enum SurfFlags : int
    {
        LIGHT = 0x1,
        SKY2D = 0x2,
        SKY = 0x4,
        WARP = 0x8,
        TRANS = 0x10,
        NOPORTAL = 0x20,
        TRIGGER = 0x40,
        NODRAW = 0x80,
        HINT = 0x100,
        SKIP = 0x200,
        NOLIGHT = 0x400,
        BUMPLIGHT = 0x800,
        NOSHADOWS = 0x1000,
        NODECALS = 0x2000,
        NOCHOP = 0x4000,
        HITBOX = 0x8000
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TexAxis
    {
        public Vector Normal;
        public float Offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureInfo
    {
        public TexAxis TextureUAxis;
        public TexAxis TextureVAxis;

        public TexAxis LightmapUAxis;
        public TexAxis LightmapVAxis;

        public SurfFlags Flags;
        public int TexData;
    }
}
