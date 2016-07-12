using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

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

            _lines = value.Split(new[] {"\r\n", "\n"}, StringSplitOptions.None).Select(x => TrimLine(x)).ToArray();
        }

        public void AssertToken(string token)
        {
            if (!ReadToken(token)) ExpectedError(string.Format("'{0}'", token));
        }

        public void AssertRegex(Regex regex, out Match match, string token)
        {
            if (!ReadRegex(regex, out match)) ExpectedError(token);
        }

        private string TrimLine(string line)
        {
            line = line.Trim();

            var comment = line.IndexOf("//");
            if (comment != -1) line = line.Substring(0, comment);

            return line;
        }

        public bool ReadToken(string token)
        {
            var curOffset = _offset;

            while (curOffset < _lines.Length)
            {
                var line = _lines[curOffset++];
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
                var line = _lines[curOffset++];
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
            throw new VmtParserException(expected, _offset, _lines[_offset]);
        }
    }

    public class PropertyGroup
    {
        private static readonly Regex _sPropertyRegex = new Regex(@"^\s*(""(?<name>[^""]+)""|(?<name>[$%a-zA-Z0-9_]+))\s+(""(?<value>[^""]+)""|(?<value>\S+))\s*$", RegexOptions.Compiled);
        private static readonly Regex _sNestedRegex = new Regex(@"^\s*(""(?<name>[^""]+)""|(?<name>[$%a-zA-Z0-9_]+))\s*$", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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

        public float GetSingle(string name, float @default = 0f)
        {
            var value = GetString(name, @default.ToString());

            float floatValue;
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatValue) ? floatValue : @default;
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
                if (reader.ReadRegex(_sNestedRegex, out match))
                {
                    // TODO
                    var nested = new PropertyGroup(reader);
                    continue;
                }

                reader.AssertRegex(_sPropertyRegex, out match, "shader property");

                var name = match.Groups["name"];
                var value = match.Groups["value"];

                _properties.Add(name.Value, value.Value);
            }
        }
    }

    public class VmtParserException : Exception
    {
        public VmtParserException(string expected, int line, string lineValue)
            : base(string.Format("Error while parsing material: expected {0} on line {1}.{2}{3}", expected, line, Environment.NewLine, lineValue)) { }
    }

    public class VmtFile
    {
        [Flags]
        private enum MaterialFlags
        {
            Default = 0,
            NoCull = 1,
            Translucent = 2,
            AlphaTest = 4,
            TreeSway = 8,
            Lightmapped = 16
        }

        private static Material _sDefaultVertLitMaterial;
        private static Material _sDefaultLightmappedMaterial;

        public static Material GetDefaultMaterial(bool lightmapped)
        {
            if (!lightmapped)
            {
                return _sDefaultVertLitMaterial ?? (_sDefaultVertLitMaterial = CreateMaterial(MaterialFlags.Default));
            }

            return _sDefaultLightmappedMaterial ?? (_sDefaultLightmappedMaterial = CreateMaterial(MaterialFlags.Lightmapped));
        }

        private static Material CreateMaterial(MaterialFlags flags)
        {
            var shaderName = ((flags & MaterialFlags.Lightmapped) == MaterialFlags.Lightmapped)
                ? "Custom/WorldGeometry"
                : "Custom/PropGeometry";

            if ((flags & MaterialFlags.NoCull) == MaterialFlags.NoCull)
            {
                shaderName += ".NoCull";
            }

            if ((flags & MaterialFlags.Translucent) == MaterialFlags.Translucent)
            {
                shaderName += ".Translucent";
            }
            else if ((flags & MaterialFlags.AlphaTest) == MaterialFlags.AlphaTest)
            {
                shaderName += ".AlphaTest";
            }

            var shader = Shader.Find(shaderName);
            if (shader == null) throw new FileNotFoundException(string.Format("Unable to find shader '{0}'", shaderName));

            var mat = new Material(shader);

            if ((flags & MaterialFlags.TreeSway) == MaterialFlags.TreeSway)
            {
                mat.EnableKeyword("TREE_SWAY");
            }

            return mat;
        }

        public static VmtFile FromStream(Stream stream)
        {
            var reader = new VmtFileReader(stream);
            var file = new VmtFile(reader);

            return file;
        }

        private readonly Dictionary<string, PropertyGroup> _propertyGroups = new Dictionary<string, PropertyGroup>(StringComparer.InvariantCultureIgnoreCase);
        private Material _material;

        private VmtFile(VmtFileReader reader)
        {
            var shaderNameRegex = new Regex(@"^\s*(""(?<shader>[a-zA-Z0-9/\\]+)""|(?<shader>[a-zA-Z0-9/\\]+))\s*$", RegexOptions.Compiled);

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

        public Material GetMaterial(ResourceLoader loader)
        {
            if (_material != null) return _material;
            if (_propertyGroups.Count == 0) return null;

            var propGroup = _propertyGroups.First();
            var name = propGroup.Key.ToLower();
            var lightmapped = name == "lightmappedgeneric" || name == "worldvertextransition";
            var alphatest = propGroup.Value.GetBoolean("$alphatest");
            var translucent = propGroup.Value.GetBoolean("$translucent");
            var nocull = propGroup.Value.GetBoolean("$nocull");
            var basetex = propGroup.Value.GetString("$basetexture");
            var treeSway = propGroup.Value.GetBoolean("$treesway");

            var flags = MaterialFlags.Default;

            if (lightmapped) flags |= MaterialFlags.Lightmapped;
            if (nocull) flags |= MaterialFlags.NoCull;
            if (alphatest) flags |= MaterialFlags.AlphaTest;
            else if (translucent) flags |= MaterialFlags.Translucent;
            if (treeSway) flags |= MaterialFlags.TreeSway;

            if (flags == MaterialFlags.Default || flags == MaterialFlags.Lightmapped || basetex == null) return _material = GetDefaultMaterial(lightmapped);

            _material = CreateMaterial(flags);

            if (flags != MaterialFlags.TreeSway)
            {
                if (!basetex.EndsWith(".vtf")) basetex += ".vtf";
                _material.mainTexture = loader.LoadVtf(basetex.Replace('\\', '/')).GetTexture();
            }

            if ((flags & MaterialFlags.TreeSway) == MaterialFlags.TreeSway)
            {
                _material.SetFloat("_TreeSwayStartHeight", propGroup.Value.GetSingle("$treeSwayStartHeight", 0.5f));
                _material.SetFloat("_TreeSwayHeight", propGroup.Value.GetSingle("$treeSwayHeight", 300f));
                _material.SetFloat("_TreeSwayStartRadius", propGroup.Value.GetSingle("$treeSwayStartRadius", 0f));
                _material.SetFloat("_TreeSwayRadius", propGroup.Value.GetSingle("$treeSwayRadius", 200f));
                _material.SetFloat("_TreeSwaySpeed", propGroup.Value.GetSingle("$treeSwaySpeed", 0.2f));
                _material.SetFloat("_TreeSwayStrength", Mathf.Min(propGroup.Value.GetSingle("$treeSwayStrength", 0.4f), 1f));
            }

            return _material;
        }
    }
}