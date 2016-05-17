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
                Lightmap = _bspFile.GenerateLightmap();
                var meshes = _bspFile.GenerateMeshes();

                Material.mainTexture = Lightmap;

                var index = 0;
                foreach (var mesh in meshes)
                {
                    var modelChild = new GameObject("Model " + index++, typeof(MeshFilter), typeof(MeshRenderer));
                    modelChild.transform.SetParent(transform, true);

                    modelChild.GetComponent<MeshFilter>().sharedMesh = mesh;
                    modelChild.GetComponent<MeshRenderer>().sharedMaterial = Material;
                }
            }
        }
    }
}