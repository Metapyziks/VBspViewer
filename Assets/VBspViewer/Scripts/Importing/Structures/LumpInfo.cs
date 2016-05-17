using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LumpInfo
    {
        public int Offset;
        public int Length;
        public int Version;
        public int IdentCode;
    }
}
