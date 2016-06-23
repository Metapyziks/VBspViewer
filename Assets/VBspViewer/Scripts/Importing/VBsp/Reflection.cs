using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using VBspViewer.Importing.VBsp.Structures;

namespace VBspViewer.Importing.VBsp
{
    internal class ReadLumpWrapper<T>
        where T : struct
    {
        public static T[] ReadLump(byte[] src, int offset, int length)
        {
            var size = Marshal.SizeOf(typeof(T));
            var count = length/size;
            var array = new T[count];

            if (typeof (T) == typeof (byte))
            {
                Array.Copy(src, array, array.Length);
                return array;
            }

            var tempPtr = Marshal.AllocHGlobal(size);
            
            for (var i = 0; i < count; ++i)
            {
                Marshal.Copy(src, offset + i * size, tempPtr, size);
                array[i] = (T) Marshal.PtrToStructure(tempPtr, typeof (T));
            }

            Marshal.FreeHGlobal(tempPtr);

            return array;
        }

        public static void ReadLumpToList(byte[] src, int offset, int length, List<T> dstList)
        {
            var size = Marshal.SizeOf(typeof(T));
            var count = length/size;

            if (typeof(T) == typeof(byte))
            {
                ((List<byte>) (object) dstList).AddRange(src);
            }

            var tempPtr = Marshal.AllocHGlobal(size);

            for (var i = 0; i < count; ++i)
            {
                Marshal.Copy(src, offset + i * size, tempPtr, size);
                dstList.Add((T) Marshal.PtrToStructure(tempPtr, typeof(T)));
            }

            Marshal.FreeHGlobal(tempPtr);
        }

        [ThreadStatic]
        private static byte[] _sReadLumpBuffer;

        [ThreadStatic]
        private static List<T> _sReadLumpList;

        public static T ReadSingleFromStream(Stream stream)
        {
            if (_sReadLumpList == null) _sReadLumpList = new List<T>();
            else _sReadLumpList.Clear();

            ReadLumpFromStream(stream, 1, _sReadLumpList);

            return _sReadLumpList[0];
        }

        public static void ReadLumpFromStream(Stream stream, int count, Action<T> handler)
        {
            if (_sReadLumpList == null) _sReadLumpList = new List<T>();
            else _sReadLumpList.Clear();

            var size = Marshal.SizeOf(typeof (T));
            var start = stream.Position;

            ReadLumpFromStream(stream, count, _sReadLumpList);

            for (var i = 0; i < count; ++i)
            {
                stream.Seek(start + i*size, SeekOrigin.Begin);
                handler(_sReadLumpList[i]);
            }
        }

        public static T[] ReadLumpFromStream(Stream stream, int count)
        {
            if (_sReadLumpList == null) _sReadLumpList = new List<T>();
            else _sReadLumpList.Clear();

            var size = Marshal.SizeOf(typeof (T));
            var start = stream.Position;

            ReadLumpFromStream(stream, count, _sReadLumpList);

            var output = new T[count];
            for (var i = 0; i < count; ++i)
            {
                output[i] = _sReadLumpList[i];
            }

            return output;
        }

        public static void ReadLumpFromStream(Stream stream, int count, List<T> dstList)
        {
            using (Profiler.Begin("ReadLumpFromStream"))
            {
                var size = Marshal.SizeOf(typeof (T));
                var length = count*size;

                if (_sReadLumpBuffer == null || _sReadLumpBuffer.Length < length)
                {
                    _sReadLumpBuffer = new byte[length];
                }

                stream.Read(_sReadLumpBuffer, 0, length);
                ReadLumpToList(_sReadLumpBuffer, 0, length, dstList);
            }
        }
    }

    public partial class VBspFile
    {
        [MeansImplicitUse, AttributeUsage(AttributeTargets.Property)]
        private class LumpAttribute : Attribute
        {
            public LumpType Type { get; set; }
            public int StartOffset { get; set; }
        }

        private delegate void ReadLumpDelegate(VBspFile file, byte[] src, int length);

        private static readonly Dictionary<Type, MethodInfo> _sReadLumpMethods = new Dictionary<Type, MethodInfo>();
        private static MethodInfo FindReadLumpMethod(Type type)
        {
            MethodInfo readLumpMethod;
            if (_sReadLumpMethods.TryGetValue(type, out readLumpMethod)) return readLumpMethod;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.Public;

            var readLumpWrapper = typeof (ReadLumpWrapper<>).MakeGenericType(type);
            var readLump = readLumpWrapper.GetMethod("ReadLump", bFlags);

            _sReadLumpMethods.Add(type, readLump);
            return readLump;
        }

        private static Dictionary<LumpType, ReadLumpDelegate> _sReadLumpDelegates;
        private static Dictionary<LumpType, ReadLumpDelegate> GetReadLumpDelegates()
        {
            if (_sReadLumpDelegates != null) return _sReadLumpDelegates;

            _sReadLumpDelegates = new Dictionary<LumpType, ReadLumpDelegate>();

            var fileParam = Expression.Parameter(typeof (VBspFile), "file");
            var srcParam = Expression.Parameter(typeof (byte[]), "src");
            var lengthParam = Expression.Parameter(typeof (int), "length");

            const BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var prop in typeof(VBspFile).GetProperties(bFlags))
            {
                var attrib = (LumpAttribute) prop.GetCustomAttributes(typeof(LumpAttribute), true).FirstOrDefault();
                if (attrib == null) continue;

                var type = prop.PropertyType.GetElementType();
                var readLumpMethod = FindReadLumpMethod(type);

                var offsetConst = Expression.Constant(attrib.StartOffset);
                var lengthVal = Expression.Subtract(lengthParam, offsetConst);
                var call = Expression.Call(readLumpMethod, srcParam, offsetConst, lengthVal);
                var set = Expression.Call(fileParam, prop.GetSetMethod(true), call);
                var lambda = Expression.Lambda<ReadLumpDelegate>(set, fileParam, srcParam, lengthParam);

                _sReadLumpDelegates.Add(attrib.Type, lambda.Compile());
            }

            return _sReadLumpDelegates;
        }
    }
}
