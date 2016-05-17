using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Plane
    {
        public static Plane Read(BinaryReader reader)
        {
            return new Plane
            {
                Normal = Vector.Read(reader),
                Distance = reader.ReadSingle(),
                Type = reader.ReadInt32()
            };
        }

        public Vector Normal;
        public float Distance;
        public int Type;
    }
}
