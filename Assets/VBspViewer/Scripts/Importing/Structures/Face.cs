using System.IO;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Face
    {
        public static Face Read(BinaryReader reader)
        {
            return new Face
            {
                PlaneNum = reader.ReadUInt16(),
                Side = (Side) reader.ReadByte(),
                OnNode = reader.ReadBoolean(),
                FirstEdge = reader.ReadInt32(),
                NumEdges = reader.ReadInt16(),
                TexInfo = reader.ReadInt16(),
                DispInfo = reader.ReadInt16(),
                FogVolumeId = reader.ReadInt16(),
                Styles = reader.ReadInt32(),
                LightOffset = reader.ReadInt32(),
                Area = reader.ReadSingle(),
                LightMapOffsetX = reader.ReadInt32(),
                LightMapOffsetY = reader.ReadInt32(),
                LightMapSizeX = reader.ReadInt32(),
                LightMapSizeY = reader.ReadInt32(),
                OriginalFace = reader.ReadInt32(),
                NumPrimitives = reader.ReadUInt16(),
                FirstPrimitive = reader.ReadUInt16(),
                SmoothingGroups = reader.ReadUInt32()
            };
        }

        public ushort PlaneNum;
        public Side Side;
        public bool OnNode;
        public int FirstEdge;
        public short NumEdges;
        public short TexInfo;
        public short DispInfo;
        public short FogVolumeId;
        public int Styles;
        public int LightOffset;
        public float Area;
        public int LightMapOffsetX;
        public int LightMapOffsetY;
        public int LightMapSizeX;
        public int LightMapSizeY;
        public int OriginalFace;
        public ushort NumPrimitives;
        public ushort FirstPrimitive;
        public uint SmoothingGroups;
    }
}
