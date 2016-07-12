using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TextureData
    {
        public Vector Reflectivity;
        public int NameStringTableId;
        public int Width, Height;
        public int ViewWidth, ViewHeight;
    }
}
