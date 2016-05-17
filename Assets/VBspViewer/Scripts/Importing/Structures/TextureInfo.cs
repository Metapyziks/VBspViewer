using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
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
        public static TexAxis Read(BinaryReader reader)
        {
            return new TexAxis
            {
                Normal = Vector.Read(reader),
                Offset = reader.ReadSingle()
            };
        }

        public Vector Normal;
        public float Offset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureInfo
    {
        public static TextureInfo Read(BinaryReader reader)
        {
            return new TextureInfo
            {
                TextureUAxis = TexAxis.Read(reader),
                TextureVAxis = TexAxis.Read(reader),

                LightmapUAxis = TexAxis.Read(reader),
                LightmapVAxis = TexAxis.Read(reader),

                Flags = (SurfFlags) reader.ReadInt32(),
                TexData = reader.ReadInt32()
            };
        }

        public TexAxis TextureUAxis;
        public TexAxis TextureVAxis;

        public TexAxis LightmapUAxis;
        public TexAxis LightmapVAxis;

        public SurfFlags Flags;
        public int TexData;
    }
}
