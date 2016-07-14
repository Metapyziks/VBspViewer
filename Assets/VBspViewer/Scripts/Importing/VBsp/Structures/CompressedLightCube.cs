using System;
using System.Runtime.InteropServices;

namespace VBspViewer.Importing.VBsp.Structures
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CompressedLightCube
    {
        public LightmapSample Sample0;
        public LightmapSample Sample1;
        public LightmapSample Sample2;
        public LightmapSample Sample3;
        public LightmapSample Sample4;
        public LightmapSample Sample5;

        public LightmapSample this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return Sample0;
                    case 1: return Sample1;
                    case 2: return Sample2;
                    case 3: return Sample3;
                    case 4: return Sample4;
                    case 5: return Sample5;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
    }
}
