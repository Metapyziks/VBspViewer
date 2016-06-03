using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Edge
    {
        public ushort A;
        public ushort B;
    }
}
