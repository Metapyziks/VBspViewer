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

            var geomParent = new GameObject("Geometry");
            geomParent.transform.SetParent(transform, true);

            var index = 0;
            foreach (var mesh in meshes)
            {
                var modelChild = new GameObject("Model " + index++, typeof(MeshFilter), typeof(MeshRenderer));
                modelChild.transform.SetParent(geomParent.transform, true);

                modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                modelChild.GetComponent<MeshRenderer>().sharedMaterial = Material;
            }

            var entParent = new GameObject("Entities");
            entParent.transform.SetParent(transform, true);

            var keyVals = new Dictionary<string, EntValue>();

            foreach (var entInfo in _bspFile.GetEntityKeyVals())
            {
                string className = null;
                string targetName = null;
                var origin = Vector3.zero;
                var angles = Quaternion.identity;
                var pitch = 0f;

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
                        case "pitch":
                            pitch = (float) keyVal.Value;
                            break;
                        default:
                            if (keyVals.ContainsKey(keyVal.Key)) continue;
                            keyVals.Add(keyVal.Key, keyVal.Value);
                            break;
                    }
                }

                var obj = new GameObject(targetName ?? className);
                obj.transform.SetParent(entParent.transform, true);

                angles *= Quaternion.AngleAxis(-pitch, Vector3.right);

                obj.transform.position = origin;
                obj.transform.rotation = angles;

                var enable = false;

                switch (className)
                {
                    case "light_environment":
                    {
                        var light = obj.AddComponent<Light>();

                        light.shadows = LightShadows.Soft;
                        light.type = LightType.Directional;

                        Material.SetColor("_AmbientColor", (Color) keyVals["_ambient"]);
                        enable = true;
                        break;
                    }
                }
                
                obj.SetActive(enable);
            }
        }
    }
}