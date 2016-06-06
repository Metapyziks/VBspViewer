using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VBspViewer
{
    public static class Config
    {
        static Config()
        {
            ResetToDefaults();

            if (!File.Exists(ConfigPath)) Save(); else Load();
        }

        private static string ConfigPath { get { return Path.Combine(Path.GetDirectoryName(Application.dataPath), "config.json"); } }

        [JsonProperty("csgo_path")]
        public static string CsgoPath { get; private set; }

        public static void ResetToDefaults()
        {
            CsgoPath = @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";
        }

        private static Dictionary<string, PropertyInfo> GetConfigProperties()
        {
            var dict = new Dictionary<string, PropertyInfo>();

            foreach (var property in typeof (Config).GetProperties(BindingFlags.Public | BindingFlags.Static))
            {
                var attrib = property
                    .GetCustomAttributes(typeof (JsonPropertyAttribute), false)
                    .Cast<JsonPropertyAttribute>()
                    .FirstOrDefault();

                if (attrib == null) continue;

                var name = attrib.PropertyName ?? property.Name;
                dict.Add(name, property);
            }

            return dict;
        }

        private static void Save()
        {
            var obj = new JObject();
            var serializer = JsonSerializer.CreateDefault();

            foreach (var pair in GetConfigProperties())
            {
                obj.Add(pair.Key, JToken.FromObject(pair.Value.GetValue(null, null), serializer));
            }

            File.WriteAllText(ConfigPath, obj.ToString());
        }

        private static void Load()
        {
            var obj = JObject.Parse(File.ReadAllText(ConfigPath));
            var serializer = JsonSerializer.CreateDefault();

            foreach (var pair in GetConfigProperties())
            {
                var token = obj[pair.Key];
                if (token == null) continue;

                pair.Value.SetValue(null, token.ToObject(pair.Value.PropertyType, serializer), null);
            }
        }
    }
}
