using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DispVert
    {
        public Vector Vector;
        public float Distance;
        public float Alpha;
    }
}
