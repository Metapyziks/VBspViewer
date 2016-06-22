using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Model
    {
        public Vector Min;
        public Vector Max;
        public Vector Origin;
        public int HeadNode;
        public int FirstFace;
        public int NumFaces;

        public override string ToString()
        {
            return string.Format("Origin: {0}, FirstFace: {1}, NumFaces: {2}", Origin, FirstFace, NumFaces);
        }
    }
}
