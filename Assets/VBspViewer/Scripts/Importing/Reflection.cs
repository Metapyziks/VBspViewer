using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using VBspViewer.Importing.Structures;

namespace VBspViewer.Importing
{
    public partial class VBspFile
    {
        [MeansImplicitUse, AttributeUsage(AttributeTargets.Property)]
        private class LumpAttribute : Attribute
        {
            public LumpType Type { get; set; }
        }

        private delegate void ReadLumpDelegate(VBspFile file, LumpInfo info, BinaryReader reader);

        private class ReadLumpWrapper<T>
            where T : struct
        {
            [UsedImplicitly]
            private static T[] ReadLump(LumpInfo info, BinaryReader reader, Func<BinaryReader, T> readFunc)
            {
                var size = Marshal.SizeOf(typeof(T));
                var count = info.Length/size;
                var array = new T[count];
                for (var i = 0; i < count; ++i)
                {
                    array[i] = readFunc(reader);
                }

                return array;
            }
        }

        private static readonly Dictionary<Type, Expression> _sReadMethods = new Dictionary<Type, Expression>();
        private static Expression FindReadMethod(Type type, ParameterExpression readerParam)
        {
            Expression readMethod;
            if (_sReadMethods.TryGetValue(type, out readMethod)) return readMethod;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.Public;
            var method = type.GetMethod("Read", bFlags, null, new[] {typeof (BinaryReader)}, null);
            if (method != null)
            {
                readMethod = Expression.Call(method, readerParam);
                return CreateReadMethod(type, readerParam, readMethod);
            }

            method = typeof(BinaryReader).GetMethod("Read" + type.Name);
            if (method != null)
            {
                readMethod = Expression.Call(readerParam, method);
                return CreateReadMethod(type, readerParam, readMethod);
            }

            throw new NotImplementedException();
        }

        private static Expression CreateReadMethod(Type type, ParameterExpression readerParam, Expression call)
        {
            var delegType = typeof (Func<,>).MakeGenericType(typeof (BinaryReader), type);
            var lambda = Expression.Lambda(delegType, call, readerParam);
            var compiled = lambda.Compile();
            var constExpr = Expression.Constant(compiled, delegType);
            _sReadMethods.Add(type, constExpr);
            return constExpr;
        }

        private static readonly Dictionary<Type, MethodInfo> _sReadLumpMethods = new Dictionary<Type, MethodInfo>();
        private static MethodInfo FindReadLumpMethod(Type type)
        {
            MethodInfo readLumpMethod;
            if (_sReadLumpMethods.TryGetValue(type, out readLumpMethod)) return readLumpMethod;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.NonPublic;

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
            var infoParam = Expression.Parameter(typeof (LumpInfo), "info");
            var readerParam = Expression.Parameter(typeof (BinaryReader), "reader");

            const BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var prop in typeof(VBspFile).GetProperties(bFlags))
            {
                var attrib = (LumpAttribute) prop.GetCustomAttributes(typeof(LumpAttribute), true).FirstOrDefault();
                if (attrib == null) continue;

                var type = prop.PropertyType.GetElementType();
                var readMethod = FindReadMethod(type, readerParam);
                var readLumpMethod = FindReadLumpMethod(type);

                var call = Expression.Call(readLumpMethod, infoParam, readerParam, readMethod);
                var set = Expression.Call(fileParam, prop.GetSetMethod(true), call);
                var lambda = Expression.Lambda<ReadLumpDelegate>(set, fileParam, infoParam, readerParam);

                _sReadLumpDelegates.Add(attrib.Type, lambda.Compile());
            }

            return _sReadLumpDelegates;
        }
    }
}
