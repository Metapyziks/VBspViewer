using System;
using System.Collections.Generic;
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
        public static T[] ReadLump(byte[] src, int length)
        {
            var size = Marshal.SizeOf(typeof(T));
            var count = length/size;
            var array = new T[count];

            var tempPtr = Marshal.AllocHGlobal(size);
            
            for (var i = 0; i < count; ++i)
            {
                Marshal.Copy(src, i * size, tempPtr, size);
                array[i] = (T) Marshal.PtrToStructure(tempPtr, typeof (T));
            }

            Marshal.FreeHGlobal(tempPtr);

            return array;
        }
    }

    public partial class VBspFile
    {
        [MeansImplicitUse, AttributeUsage(AttributeTargets.Property)]
        private class LumpAttribute : Attribute
        {
            public LumpType Type { get; set; }
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

                var call = Expression.Call(readLumpMethod, srcParam, lengthParam);
                var set = Expression.Call(fileParam, prop.GetSetMethod(true), call);
                var lambda = Expression.Lambda<ReadLumpDelegate>(set, fileParam, srcParam, lengthParam);

                _sReadLumpDelegates.Add(attrib.Type, lambda.Compile());
            }

            return _sReadLumpDelegates;
        }
    }
}
