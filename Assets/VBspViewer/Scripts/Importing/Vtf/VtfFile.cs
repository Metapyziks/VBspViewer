using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using VBspViewer.Importing.VBsp;

namespace VBspViewer.Importing.Vtf
{
    public class VtfFile
    {
        private enum Flags : uint
        {
            POINTSAMPLE = 0x00000001,
            TRILINEAR = 0x00000002,
            CLAMPS = 0x00000004,
            CLAMPT = 0x00000008,
            ANISOTROPIC = 0x00000010,
            HINT_DXT5 = 0x00000020,
            PWL_CORRECTED = 0x00000040,
            NORMAL = 0x00000080,
            NOMIP = 0x00000100,
            NOLOD = 0x00000200,
            ALL_MIPS = 0x00000400,
            PROCEDURAL = 0x00000800,
            
            ONEBITALPHA = 0x00001000,
            EIGHTBITALPHA = 0x00002000,
            
            ENVMAP = 0x00004000,
            RENDERTARGET = 0x00008000,
            DEPTHRENDERTARGET = 0x00010000,
            NODEBUGOVERRIDE = 0x00020000,
            SINGLECOPY = 0x00040000,
            PRE_SRGB = 0x00080000,

            UNUSED_00100000 = 0x00100000,
            UNUSED_00200000 = 0x00200000,
            UNUSED_00400000 = 0x00400000,

            NODEPTHBUFFER = 0x00800000,

            UNUSED_01000000 = 0x01000000,

            CLAMPU = 0x02000000,
            VERTEXTEXTURE = 0x04000000,
            SSBUMP = 0x08000000,

            UNUSED_10000000 = 0x10000000,

            BORDER = 0x20000000,

            UNUSED_40000000 = 0x40000000,
            UNUSED_80000000 = 0x80000000
        }

        private enum Format : uint
        {
            NONE = 0xffffffff,
            RGBA8888 = 0,
            ABGR8888,
            RGB888,
            BGR888,
            RGB565,
            I8,
            IA88,
            P8,
            A8,
            RGB888_BLUESCREEN,
            BGR888_BLUESCREEN,
            ARGB8888,
            BGRA8888,
            DXT1,
            DXT3,
            DXT5,
            BGRX8888,
            BGR565,
            BGRX5551,
            BGRA4444,
            DXT1_ONEBITALPHA,
            BGRA5551,
            UV88,
            UVWQ8888,
            RGBA16161616F,
            RGBA16161616,
            UVLX8888
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public int Signature;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint HeaderSize;
            public ushort Width;
            public ushort Height;
            public Flags Flags;
            public ushort Frames;
            public ushort FirstFrame;
            private int _padding0;
            public float ReflectivityR;
            public float ReflectivityG;
            public float ReflectivityB;
            private int _padding1;
            public float BumpmapScale;
            public Format HiResFormat;
            public byte MipMapCount;
            public Format LowResFormat;
            public byte LowResWidth;
            public byte LowResHeight;
            public ushort Depth;
        }

        public static VtfFile FromStream(Stream stream)
        {
            return new VtfFile(stream);
        }

        private static int GetImageDataSize(int width, int height, int depth, int mipCount, Format format)
        {
            if (mipCount == 0) return 0;

            var toAdd = 0;
            if (mipCount > 1) toAdd += GetImageDataSize(width >> 1, height >> 1, depth, mipCount - 1, format);
            
            // TODO: move this when supporting non-DXT formats
            if (width < 4 && width > 0) width = 4;
            if (height < 4 && height > 0) height = 4;

            switch (format)
            {
                case Format.NONE: return toAdd;
                case Format.DXT1: return toAdd + ((width*height) >> 1) * depth;
                case Format.DXT5: return toAdd + width * height*depth;
                default: throw new NotImplementedException();
            }
        }

        private readonly Header _header;
        private readonly Texture2D _texture;

        private VtfFile(Stream stream)
        {
            _header = ReadLumpWrapper<Header>.ReadSingleFromStream(stream);

            stream.Seek(_header.HeaderSize, SeekOrigin.Begin);

            var thumbSize = GetImageDataSize(_header.LowResWidth, _header.LowResHeight, 1, 1, _header.LowResFormat);

            TextureFormat unityFormat;
            switch (_header.HiResFormat)
            {
                case Format.DXT1: unityFormat = TextureFormat.DXT1; break;
                case Format.DXT5: unityFormat = TextureFormat.DXT5; break;
                default: throw new NotImplementedException(string.Format("VTF format: {0}", _header.HiResFormat));
            }
            
            stream.Seek(thumbSize, SeekOrigin.Current);
            
            var totalSize = GetImageDataSize(_header.Width, _header.Height, 1, _header.MipMapCount, _header.HiResFormat);

            var buffer = new byte[totalSize];
            var width = _header.Width;
            var height = _header.Height;

            var start = stream.Position;
            var offset = totalSize;
            var writePos = 0;

            for (var i = 0; i < _header.MipMapCount; ++i)
            {
                var size = GetImageDataSize(width, height, 1, 1, _header.HiResFormat);

                offset -= size;

                stream.Seek(start + offset, SeekOrigin.Begin);
                stream.Read(buffer, writePos, size);

                writePos += size;

                width >>= 1;
                height >>= 1;
            }

            try
            {
                _texture = new Texture2D(_header.Width, _header.Height, unityFormat, _header.MipMapCount > 1);
                _texture.LoadRawTextureData(buffer);
                _texture.Apply();
            }
            catch (UnityException e)
            {
                Debug.LogError(e);
                _texture = null;
            }
        }

        public Texture GetTexture()
        {
            return _texture;
        }
    }
}