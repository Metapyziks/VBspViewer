using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using VBspViewer.Importing;
using VBspViewer.Importing.Dem;
using VBspViewer.Importing.VBsp;
using VBspViewer.Importing.Entities;
using VBspViewer.Importing.Vpk;

namespace VBspViewer.Behaviours.Entities
{
    [ClassName(ClassName = "World")]
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

        private DemFile _demFile;
        public VBspFile BspFile { get; private set; }

        protected override void OnStart()
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
                BspFile = new VBspFile(stream);
            }
            
            Resources.AddResourceProvider(BspFile.PakFile);

            using (Profiler.Begin("CreateEntities"))
            {
                var infos = _demFile == null
                    ? BspFile.GetEntityKeyVals().Concat(BspFile.GetStaticPropKeyVals())
                    : BspFile.GetStaticPropKeyVals();

                foreach (var entInfo in infos)
                {
                    EntityManager.CreateEntity(entInfo);
                }
            }

            ModelIndex = 0;

            base.OnStart();

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
            if (BspFile != null) Resources.RemoveResourceProvider(BspFile.PakFile);

            if (_demFile != null)
            {
                _demFile.Dispose();
                _demFile = null;
            }
        }
    }
}