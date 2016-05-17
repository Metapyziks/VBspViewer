using UnityEngine;
using System.IO;
using JetBrains.Annotations;
using VBspViewer.Importing;

namespace VBspViewer.Behaviours
{
    public class Map : MonoBehaviour
    {
        public string FilePath;
        public Material Material;
        
        public Texture2D Lightmap;

        private VBspFile _bspFile;

        [UsedImplicitly]
        private void Start()
        {
            var filePath = Path.Combine(@"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\csgo\maps", FilePath);

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            using (var stream = File.OpenRead(filePath))
            {
                _bspFile = new VBspFile(stream);
                var mesh = _bspFile.GenerateMesh();
                Lightmap = _bspFile.GenerateLightmap();

                var modelChild = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
                modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                modelChild.GetComponent<MeshRenderer>().sharedMaterial = Material;
            }
        }
    }
}