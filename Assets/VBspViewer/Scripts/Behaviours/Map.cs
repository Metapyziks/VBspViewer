﻿using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Assets.VBspViewer.Scripts.Importing.VBsp;
using JetBrains.Annotations;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem;
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

        public string DemoName;
        public string MapName;
        public bool LoadMap = true;
        public bool DemoPlayback = false;
        
        public int CurrentTick;

        [Range(0f, 8f)]
        public float DemoTimescale = 1f;

        public Material WorldMaterial;

        public Texture2D Lightmap;

        private DemFile _demFile;
        private VBspFile _bspFile;

        [UsedImplicitly]
        private void Start()
        {
            if (!string.IsNullOrEmpty(DemoName))
            {
                var demPath = Path.Combine(Config.CsgoPath, Path.Combine("replays", DemoName + ".dem"));
                if (File.Exists(demPath))
                {
                    _demFile = new DemFile(File.OpenRead(demPath));
                    MapName = _demFile.MapName;
                }
            }

            if (_demFile != null)
            {
                _demFile.Initialize();
            }

            if (!LoadMap) return;

            var filePath = Path.Combine(Config.CsgoPath, Path.Combine("maps", MapName + ".bsp"));

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

            WorldMaterial.SetTexture("_LightMap", Lightmap);

            var keyVals = new Dictionary<string, EntValue>();

            using (Profiler.Begin("CreateEntities"))
            {
                foreach (var entInfo in _bspFile.GetEntityKeyVals())
                {
                    string className = null;
                    string targetName = null;
                    string modelName = null;
                    var origin = Vector3.zero;
                    var angles = Quaternion.identity;
                    var pitch = 0f;
                    var modelIndex = -1;

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
                            case "model":
                                modelName = (string) keyVal.Value;
                                if (modelName.StartsWith("*")) modelIndex = int.Parse(modelName.Substring(1));
                                break;
                            default:
                                if (keyVals.ContainsKey(keyVal.Key)) continue;
                                keyVals.Add(keyVal.Key, keyVal.Value);
                                break;
                        }
                    }

                    var obj = new GameObject(targetName ?? className);
                    obj.transform.SetParent(transform, true);

                    angles *= Quaternion.AngleAxis(-pitch, Vector3.right);

                    obj.transform.position = origin;
                    obj.transform.rotation = angles;
                    
                    var enable = false;

                    switch (className)
                    {
                        case "worldspawn":
                        {
                            modelIndex = 0;
                            enable = true;
                            obj.isStatic = true;

                            break;
                        }
                        case "light_environment":
                        {
                            var light = obj.AddComponent<Light>();
                            var color = (Color) keyVals["_light"];
                            var ambient = (Color) keyVals["_ambient"];

                            var pow = 0.25f;

                            ambient = new Color(Mathf.Pow(ambient.r, pow), Mathf.Pow(ambient.g, pow), Mathf.Pow(ambient.b, pow));

                            light.color = color;
                            light.intensity = 1f;
                            light.shadows = LightShadows.Soft;
                            light.type = LightType.Directional;

                            RenderSettings.ambientLight = ambient;
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
                                
                            prop.Unknown = (string) keyVals["unknown"];
                            prop.VertexLighting = (string) keyVals["vlighting"];
                            prop.SetFlags((int) keyVals["flags"]);
                            prop.SetModel(modelName);

                            obj.isStatic = true;
                            enable = true;

                            break;
                        }
                    }

                    if (modelIndex >= 0)
                    {
                        var meshes = _bspFile.GenerateMeshes(modelIndex);
                        foreach (var mesh in meshes)
                        {
                            if (mesh.vertexCount == 0) continue;

                            var modelChild = new GameObject("faces", typeof(MeshFilter), typeof(MeshRenderer));
                            modelChild.transform.SetParent(obj.transform, false);

                            modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                            modelChild.GetComponent<MeshRenderer>().sharedMaterial = WorldMaterial;
                            modelChild.isStatic = modelIndex == 0;
                        }

                        enable = true;
                    }

                    obj.SetActive(enable);
                }
            }

            Profiler.Print();
        }

        [UsedImplicitly]
        private void Update()
        {
            if (_demFile != null && DemoPlayback)
            {
                _demFile.Update(Time.deltaTime * DemoTimescale);
                CurrentTick = _demFile.CurrentTick;
            }
        }

        [UsedImplicitly]
        private void OnDestroy()
        {
            if (_bspFile != null) Resources.RemoveResourceProvider(_bspFile.PakFile);

            if (_demFile != null)
            {
                _demFile.Dispose();
                _demFile = null;
            }
        }
    }
}