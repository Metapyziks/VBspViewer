using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace VBspViewer.Importing.Vmt
{
    public class VmtFileReader
    {
        private readonly string[] _lines;
        private int _offset;

        public VmtFileReader(Stream stream)
        {
            var reader = new StreamReader(stream);
            var value = reader.ReadToEnd();

            _lines = value.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None);
        }

        public void AssertToken(string token)
        {
            if (!ReadToken(token)) ExpectedError(string.Format("'{0}'", token));
        }

        public void AssertRegex(Regex regex, out Match match, string token)
        {
            if (!ReadRegex(regex, out match)) ExpectedError(token);
        }

        public bool ReadToken(string token)
        {
            var curOffset = _offset;

            while (curOffset < _lines.Length)
            {
                var line = _lines[curOffset++].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (!line.Equals(token)) return false;

                _offset = curOffset;
                return true;
            }

            return false;
        }

        public bool ReadRegex(Regex regex, out Match match)
        {
            var curOffset = _offset;

            match = null;
            while (curOffset < _lines.Length)
            {
                var line = _lines[curOffset++].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                match = regex.Match(line);
                if (!match.Success) return false;

                _offset = curOffset;
                return true;
            }

            return false;
        }

        public void ExpectedError(string expected)
        {
            throw new VmtParserException(expected, _offset);
        }
    }

    public class PropertyGroup
    {
        private static readonly Regex _sPropertyRegex = new Regex(@"^\s*((?<name>\$[a-zA-Z0-9_]+)|""(?<name>[^""]+)"")\s+((?<value>\S+)|""(?<value>[^""]+)"")\s*$", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>();

        public string GetString(string name, string @default = null)
        {
            string value;
            return !_properties.TryGetValue(name, out value) ? null : value;
        }

        public int GetInt32(string name, int @default = 0)
        {
            var value = GetString(name, @default.ToString());

            int intValue;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue) ? intValue : @default;
        }

        public bool GetBoolean(string name, bool @default = false)
        {
            var value = GetInt32(name, @default ? 1 : 0);
            return value != 0;
        }

        public PropertyGroup(VmtFileReader reader)
        {
            reader.AssertToken("{");

            while (!reader.ReadToken("}"))
            {
                Match match;
                reader.AssertRegex(_sPropertyRegex, out match, "shader property");
                _properties.Add(match.Groups["name"].Value, match.Groups["value"].Value);
            }
        }
    }

    public class VmtParserException : Exception
    {
        public VmtParserException(string expected, int line)
            : base(string.Format("Error while parsing material: expected {0} on line {1}.", expected, line)) { }
    }

    public class VmtFile
    {
        public static VmtFile FromStream(Stream stream)
        {
            var reader = new VmtFileReader(stream);
            var file = new VmtFile(reader);

            return file;
        }

        private readonly Dictionary<string, PropertyGroup> _propertyGroups = new Dictionary<string, PropertyGroup>();

        private VmtFile(VmtFileReader reader)
        {
            var shaderNameRegex = new Regex(@"^\s*(?<shader>[a-zA-Z0-9/\\]+)\s*$", RegexOptions.Compiled);

            Match match;
            while (reader.ReadRegex(shaderNameRegex, out match))
            {
                var shader = match.Groups["shader"].Value;
                var group = new PropertyGroup(reader);

                _propertyGroups.Add(shader, group);
            }
        }

        public IEnumerable<string> Shaders { get { return _propertyGroups.Keys; } } 

        public bool ContainsShader(string shader)
        {
            return _propertyGroups.ContainsKey(shader);
        }

        public PropertyGroup this[string shader]
        {
            get { return _propertyGroups[shader]; }
        }
    }
}