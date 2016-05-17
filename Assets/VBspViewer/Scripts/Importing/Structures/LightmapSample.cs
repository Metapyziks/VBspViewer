using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LightmapSample
    {
        public static LightmapSample Read(BinaryReader reader)
        {
            return new LightmapSample
            {
                R = reader.ReadByte(),
                G = reader.ReadByte(),
                B = reader.ReadByte(),
                Exponent = reader.ReadSByte()
            };
        }

        public static implicit operator Color(LightmapSample sample)
        {
            var mul = Mathf.Pow(2f, sample.Exponent) / 256f;
            return new Color(sample.R * mul, sample.G * mul, sample.B * mul, 1f);
        }

        public byte R;
        public byte G;
        public byte B;
        public sbyte Exponent;
    }
}
