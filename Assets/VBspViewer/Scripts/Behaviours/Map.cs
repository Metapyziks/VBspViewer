using System.Collections.Generic;
using UnityEngine;
using System.IO;
using JetBrains.Annotations;
using VBspViewer.Importing;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Vpk;

namespace VBspViewer.Behaviours
{
    public class Map : MonoBehaviour
    {
        public string FilePath;
        public Material WorldMaterial;
        public Material PropMaterial;

        public Texture2D Lightmap;

        private VBspFile _bspFile;
        private readonly ResourceLoader _resLoader = new ResourceLoader();

        private MeshFilter _primitiveProvider;

        [UsedImplicitly]
        private void Start()
        {
            var filePath = Path.Combine(Config.CsgoPath, Path.Combine("maps", FilePath));
            var vpkDirPath = Path.Combine(Config.CsgoPath, "pak01_dir.vpk");

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Could not find map file.", filePath);
            }

            using (var stream = File.OpenRead(filePath))
            {
                _bspFile = new VBspFile(stream);
            }

            _resLoader.AddResourceProvider(new VpkArchve(vpkDirPath));

            Lightmap = _bspFile.GenerateLightmap();
            var meshes = _bspFile.GenerateMeshes();

            WorldMaterial.SetTexture("_LightMap", Lightmap);

            var geomParent = new GameObject("Geometry");
            geomParent.transform.SetParent(transform, true);

            var index = 0;
            foreach (var mesh in meshes)
            {
                var modelChild = new GameObject("Model " + index++, typeof(MeshFilter), typeof(MeshRenderer));
                modelChild.transform.SetParent(geomParent.transform, true);

                modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                modelChild.GetComponent<MeshRenderer>().sharedMaterial = WorldMaterial;
            }

            var entParent = new GameObject("Entities");
            entParent.transform.SetParent(transform, true);

            var keyVals = new Dictionary<string, EntValue>();

            _primitiveProvider = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>();

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

                        WorldMaterial.SetColor("_AmbientColor", (Color) keyVals["_ambient"]);
                        enable = true;
                        break;
                    }
                    case "prop_static":
                    {
                        var modelName = (string) keyVals["model"];

                        try
                        {
                            var mdl = _resLoader.LoadMdl(modelName);

                            var mf = obj.AddComponent<MeshFilter>();
                            var mr = obj.AddComponent<MeshRenderer>();

                            mf.sharedMesh = mdl.GetMesh(0);
                            mr.sharedMaterial = PropMaterial;

                            enable = true;
                        }
                        catch (FileNotFoundException e)
                        {
                            Debug.LogWarningFormat("Unable to load model '{0}'", modelName);
                        }

                        break;
                    }
                }
                
                obj.SetActive(enable);
            }
        }
    }
}