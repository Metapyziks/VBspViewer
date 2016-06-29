using System.Collections.Generic;
using UnityEngine;
using System.IO;
using JetBrains.Annotations;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Vpk;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(HammerName = "worldspawn", ClassName = "World")]
    public class World : BrushEntity
    {
        private static bool _sLoadedGameArchive;
        private static readonly ResourceLoader _sResources = new ResourceLoader();

        public static ResourceLoader Resources
        {
            get
            {
                if (!_sLoadedGameArchive) LoadGameArchive();
                return _sResources;
                
            }
        }

        private static void LoadGameArchive()
        {
            if (_sLoadedGameArchive) return;
            _sLoadedGameArchive = true;

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

        [Range(0f, 8f)]
        public float DemoTimescale = 1f;

        public NetClient NetClient;
        public EntityManager EntityManager;

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
                    _demFile = new DemFile(File.OpenRead(demPath), NetClient);
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

            using (Profiler.Begin("CreateEntities"))
            {
                foreach (var entInfo in _bspFile.GetEntityKeyVals())
                {
                    var ent = EntityManager.CreateEntity(entInfo);
                    if (ent == null) continue;

                    var brushEnt = ent as BrushEntity;
                    if (brushEnt != null && brushEnt.ModelIndex >= 0)
                    {
                        var meshes = _bspFile.GenerateMeshes(brushEnt.ModelIndex);
                        foreach (var mesh in meshes)
                        {
                            if (mesh.vertexCount == 0) continue;

                            var modelChild = new GameObject("faces", typeof(MeshFilter), typeof(MeshRenderer));
                            modelChild.transform.SetParent(ent.transform, false);

                            modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                            modelChild.GetComponent<MeshRenderer>().sharedMaterial = WorldMaterial;
                            modelChild.isStatic = brushEnt.ModelIndex == 0;
                        }
                    }
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