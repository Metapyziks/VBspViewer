using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Assets.VBspViewer.Scripts.Importing.VBsp;
using JetBrains.Annotations;
using VBspViewer.Importing;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Vpk;

namespace VBspViewer.Behaviours
{
    public class Map : MonoBehaviour
    {
        private static readonly ResourceLoader _sResources = new ResourceLoader();
        public static ResourceLoader Resources { get { return _sResources; } }

        static Map()
        {
            var vpkDirPath = Path.Combine(Config.CsgoPath, "pak01_dir.vpk");

            using (Profiler.Begin("OpenVpkArchive"))
            {
                Resources.AddResourceProvider(new VpkArchve(vpkDirPath));
            }
        }

        public string FilePath;
        public Material WorldMaterial;
        public Material PropMaterial;

        public Texture2D Lightmap;

        private VBspFile _bspFile;

        [UsedImplicitly]
        private void Start()
        {
            var filePath = Path.Combine(Config.CsgoPath, Path.Combine("maps", FilePath));

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new FileNotFoundException("Could not find map file.", filePath);
            }

            using (var stream = File.OpenRead(filePath))
            {
                _bspFile = new VBspFile(stream);
            }
            
            Resources.AddResourceProvider(_bspFile.PakFile);

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
                modelChild.isStatic = true;
            }

            var entParent = new GameObject("Entities");
            entParent.transform.SetParent(transform, true);

            var keyVals = new Dictionary<string, EntValue>();

            using (Profiler.Begin("CreateEntities"))
            {
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
                            var color = (Color) keyVals["_light"];
                            var ambient = (Color) keyVals["_ambient"];

                            var pow = ambient.a;

                            ambient = new Color(Mathf.Pow(ambient.r, pow), Mathf.Pow(ambient.g, pow), Mathf.Pow(ambient.b, pow));

                            light.color = color;
                            light.intensity = 1f;
                            light.shadows = LightShadows.Soft;
                            light.type = LightType.Directional;

                            const float colorPow = 1f;
                            const float colorScale = 0.75f;

                            RenderSettings.ambientLight = new Color(
                                Mathf.Pow(ambient.r, colorPow) * colorScale,
                                Mathf.Pow(ambient.g, colorPow) * colorScale,
                                Mathf.Pow(ambient.b, colorPow) * colorScale);
                            DynamicGI.UpdateEnvironment();

                            WorldMaterial.SetColor("_AmbientColor", ambient);
                            enable = true;
                            break;
                        }
                        //case "light":
                        //{
                        //    var light = obj.AddComponent<Light>();
                        //    var color = (Color) keyVals["_light"];

                        //    light.color = color;
                        //    light.intensity = 1f;
                        //    light.bounceIntensity = 0f;
                        //    light.range = Mathf.Sqrt(color.a) * 255f * VBspFile.SourceToUnityUnits * 8f;

                        //    enable = true;
                        //    break;
                        //}
                        case "prop_static":
                        {
                            var prop = obj.AddComponent<PropStatic>();

                            prop.Renderer.sharedMaterial = PropMaterial;
                            prop.Unknown = (string) keyVals["unknown"];
                            prop.VertexLighting = (string) keyVals["vlighting"];
                            prop.SetFlags((int) keyVals["flags"]);
                            prop.SetModel((string) keyVals["model"]);

                            obj.isStatic = true;
                            enable = true;

                            break;
                        }
                    }

                    obj.SetActive(enable);
                }
            }

            Profiler.Print();
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            Resources.RemoveResourceProvider(_bspFile.PakFile);
        }
    }
}