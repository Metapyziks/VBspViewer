using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;

namespace VBspViewer.Importing.Entities
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EntValueAttribute : Attribute
    {
        public string Pattern { get; set; }
        public int Order { get; set; }

        public EntValueAttribute(string pattern, int order)
        {
            Pattern = pattern;
            Order = order;
        }
    }

    public abstract class EntValue
    {
        protected static readonly CultureInfo CultureInfo = CultureInfo.GetCultureInfo("en-US");

        public static explicit operator string(EntValue value)
        {
            return value.ToString();
        }

        public static explicit operator int(EntValue value)
        {
            return (int) value.ToDouble();
        }

        public static explicit operator double(EntValue value)
        {
            return value.ToDouble();
        }

        public static explicit operator Vector2(EntValue value)
        {
            return value.ToVector2();
        }

        public static explicit operator Vector3(EntValue value)
        {
            return value.ToVector3();
        }

        public static explicit operator Vector4(EntValue value)
        {
            return value.ToVector4();
        }
        
        public static explicit operator Quaternion(EntValue value)
        {
            return value.ToQuaternion();
        }

        [UsedImplicitly]
        private static EntValue Parse<TEntValue>(Match match)
            where TEntValue : EntValue, new()
        {
            var parsed = new TEntValue();
            parsed.OnParse(match);
            return parsed;
        }

        private delegate EntValue ParseDelegate(Match match);

        private class Parser
        {
            public readonly Regex Regex;
            public readonly int Order;
            public readonly ParseDelegate Delegate;

            public Parser(Regex regex, int order, ParseDelegate deleg)
            {
                Regex = regex;
                Order = order;
                Delegate = deleg;
            }
        }

        private static List<Parser> _sParsers;
        private static IEnumerable<Parser> GetParsers()
        {
            if (_sParsers != null) return _sParsers;

            const BindingFlags bFlags = BindingFlags.Static | BindingFlags.NonPublic;

            var matchParam = Expression.Parameter(typeof (Match), "match");
            var parseMethodGeneric = typeof (EntValue).GetMethod("Parse", bFlags);

            _sParsers = new List<Parser>();
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!typeof (EntValue).IsAssignableFrom(type)) continue;

                var attrib = (EntValueAttribute) type.GetCustomAttributes(typeof (EntValueAttribute), false).FirstOrDefault();
                if (attrib == null) continue;

                var parseMethod = parseMethodGeneric.MakeGenericMethod(type);
                var callExpr = Expression.Call(parseMethod, matchParam);
                var lambda = Expression.Lambda<ParseDelegate>(callExpr, matchParam);

                _sParsers.Add(new Parser(new Regex("^" + attrib.Pattern + "$"), attrib.Order, lambda.Compile()));
            }

            _sParsers.Sort((a, b) => a.Order - b.Order);

            return _sParsers;
        }

        public static EntValue Parse(string value)
        {
            foreach (var parser in GetParsers())
            {
                var match = parser.Regex.Match(value);
                if (!match.Success) continue;

                return parser.Delegate(match);
            }

            throw new NotImplementedException();
        }

        protected abstract void OnParse(Match match);

        protected virtual double ToDouble() { return ToVector4().x; }
        protected virtual Vector2 ToVector2() { return ToVector4(); }
        protected virtual Vector3 ToVector3() { return ToVector4(); }
        protected virtual Vector4 ToVector4() { return new Vector4(float.NaN, float.NaN, float.NaN, float.NaN); }
        protected virtual Quaternion ToQuaternion() { return new Quaternion(float.NaN, float.NaN, float.NaN, float.NaN); }
    }

    [EntValue(Pattern, int.MaxValue)]
    public class StringValue : EntValue
    {
        public const string Pattern = ".*";

        public string Value { get; private set; }

        protected override void OnParse(Match match)
        {
            Value = match.Value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    [EntValue(Pattern, 5)]
    public class NumberValue : EntValue
    {
        public const string Pattern = "-?[0-9]+(\\.[0-9]+)?([eE]-?[0-9]+)?";

        public double Value { get; private set; }

        protected override void OnParse(Match match)
        {
            Value = double.Parse(match.Value, CultureInfo);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo);
        }

        protected override double ToDouble()
        {
            return Value;
        }

        protected override Vector4 ToVector4()
        {
            return new Vector4((float) Value, 0f, 0f, 0f);
        }

        protected override Quaternion ToQuaternion()
        {
            return Quaternion.Euler(0f, (float) Value, 0f);
        }
    }

    [EntValue(Pattern, 4)]
    public class Vector2Value : EntValue
    {
        public const string Pattern = "(\\[\\s*)?(?<x>" + NumberValue.Pattern
            + ")\\s+(?<y>" + NumberValue.Pattern
            + ")(\\s*\\])?";

        public Vector2 Value { get; private set; }

        protected override void OnParse(Match match)
        {
            Value = new Vector2(
                float.Parse(match.Groups["x"].Value, CultureInfo),
                float.Parse(match.Groups["y"].Value, CultureInfo));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        protected override Vector4 ToVector4()
        {
            return Value;
        }

        protected override Quaternion ToQuaternion()
        {
            return Quaternion.Euler(Value.x, Value.y, 0f);
        }
    }

    [EntValue(Pattern, 3)]
    public class Vector3Value : EntValue
    {
        public const string Pattern = "(\\[\\s*)?(?<x>" + NumberValue.Pattern
            + ")\\s+(?<z>" + NumberValue.Pattern
            + ")\\s+(?<y>" + NumberValue.Pattern
            + ")(\\s*\\])?";

        public Vector3 Value { get; private set; }

        protected override void OnParse(Match match)
        {
            Value = new Vector3(
                float.Parse(match.Groups["x"].Value, CultureInfo),
                float.Parse(match.Groups["y"].Value, CultureInfo),
                float.Parse(match.Groups["z"].Value, CultureInfo));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        protected override Vector4 ToVector4()
        {
            return Value;
        }

        protected override Quaternion ToQuaternion()
        {
            return Quaternion.Euler(new Vector3(Value.x, Value.z, Value.y));
        }
    }

    [EntValue(Pattern, 2)]
    public class Vector4Value : EntValue
    {
        public const string Pattern = "(\\[\\s*)?(?<x>" + NumberValue.Pattern
            + ")\\s+(?<z>" + NumberValue.Pattern
            + ")\\s+(?<y>" + NumberValue.Pattern
            + ")\\s+(?<w>" + NumberValue.Pattern
            + ")(\\s*\\])?";

        public Vector4 Value { get; private set; }

        protected override void OnParse(Match match)
        {
            Value = new Vector4(
                float.Parse(match.Groups["x"].Value, CultureInfo),
                float.Parse(match.Groups["y"].Value, CultureInfo),
                float.Parse(match.Groups["z"].Value, CultureInfo),
                float.Parse(match.Groups["w"].Value, CultureInfo));
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        protected override Vector4 ToVector4()
        {
            return Value;
        }

        protected override Quaternion ToQuaternion()
        {
            return Quaternion.Euler(new Vector3(Value.x, Value.z, Value.y));
        }
    }
}
