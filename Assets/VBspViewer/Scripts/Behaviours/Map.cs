using System.Collections.Generic;
using UnityEngine;
using System.IO;
using JetBrains.Annotations;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Vpk;

namespace VBspViewer.Behaviours
{
    public class Map : MonoBehaviour
    {
        private const string GamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\csgo";

        public string FilePath;
        public Material Material;
        
        public Texture2D Lightmap;

        private VBspFile _bspFile;
        private VpkArchve _vpkArchive;

        [UsedImplicitly]
        private void Start()
        {
            var filePath = Path.Combine(GamePath, Path.Combine("maps", FilePath));
            var vpkDirPath = Path.Combine(GamePath, "pak01_dir.vpk");

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Could not find map file.", filePath);
            }

            using (var stream = File.OpenRead(filePath))
            {
                _bspFile = new VBspFile(stream);
            }

            _vpkArchive = new VpkArchve(vpkDirPath);

            Lightmap = _bspFile.GenerateLightmap();
            var meshes = _bspFile.GenerateMeshes();

            Material.SetTexture("_LightMap", Lightmap);

            var index = 0;
            foreach (var mesh in meshes)
            {
                var modelChild = new GameObject("Model " + index++, typeof(MeshFilter), typeof(MeshRenderer));
                modelChild.transform.SetParent(transform, true);

                modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                modelChild.GetComponent<MeshRenderer>().sharedMaterial = Material;
            }

            var keyVals = new Dictionary<string, EntValue>();

            foreach (var entInfo in _bspFile.GetEntityKeyVals())
            {
                string className = null;
                string targetName = null;
                var origin = Vector3.zero;
                var angles = Quaternion.identity;

                keyVals.Clear();

                foreach (var keyVal in entInfo)
                {
                    switch (keyVal.Key)
                    {
                        case "classname":
                            className = (string) keyVal.Value;
                            break;
                        case "targetname":
                            targetName = (string) keyVal.Value;
                            break;
                        case "origin":
                            origin = (Vector3) keyVal.Value * VBspFile.SourceToUnityUnits;
                            break;
                        case "angles":
                            angles = (Quaternion) keyVal.Value;
                            break;
                        default:
                            if (keyVals.ContainsKey(keyVal.Key)) continue;
                            keyVals.Add(keyVal.Key, keyVal.Value);
                            break;
                    }
                }

                switch (className)
                {
                    case "light_environment":
                    {
                        var pitch = (float) keyVals["pitch"];
                        angles *= Quaternion.AngleAxis(-pitch, Vector3.right);

                        var obj = new GameObject(targetName ?? className);
                        var light = obj.AddComponent<Light>();

                        light.shadows = LightShadows.Soft;
                        light.type = LightType.Directional;
                        light.transform.position = origin;
                        light.transform.rotation = angles;

                        Material.SetColor("_AmbientColor", (Color) keyVals["_ambient"]);

                        break;
                    }
                }
            }
        }
    }
}