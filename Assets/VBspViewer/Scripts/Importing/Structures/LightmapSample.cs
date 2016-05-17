using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VBspViewer.Importing.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LightmapSample
    {
        public static implicit operator Color(LightmapSample sample)
        {
            var mul = (float) Math.Pow(2d, sample.Exponent) / 255f;
            return new Color(
                Mathf.Clamp(Mathf.Pow(mul * sample.R, 0.6f), 0f, 1f),
                Mathf.Clamp(Mathf.Pow(mul * sample.G, 0.6f), 0f, 1f),
                Mathf.Clamp(Mathf.Pow(mul * sample.B, 0.6f), 0f, 1f),
                1f);
        }

        public byte R;
        public byte G;
        public byte B;
        public sbyte Exponent;
    }
}
