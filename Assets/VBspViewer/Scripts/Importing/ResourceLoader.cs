using System;
using System.Collections.Generic;
using System.IO;
using VBspViewer.Importing.Mdl;

namespace VBspViewer.Importing
{
    public interface IResourceProvider
    {
        bool ContainsFile(string filename);
        Stream OpenFile(string filename);
    }

    public class ResourceLoader : IResourceProvider
    {
        private readonly List<IResourceProvider> _providers = new List<IResourceProvider>();

        public void AddResourceProvider(IResourceProvider provider)
        {
            _providers.Add(provider);
        }

        public bool ContainsFile(string filename)
        {
            for (var i = _providers.Count - 1; i >= 0; --i)
            {
                if (_providers[i].ContainsFile(filename)) return true;
            }

            return false;
        }

        public Stream OpenFile(string filename)
        {
            for (var i = _providers.Count - 1; i >= 0; --i)
            {
                if (_providers[i].ContainsFile(filename)) return _providers[i].OpenFile(filename);
            }

            throw new FileNotFoundException();
        }

        private readonly Dictionary<string, MdlFile> _sLoadedMdls
            = new Dictionary<string, MdlFile>(StringComparer.CurrentCultureIgnoreCase);

        public MdlFile LoadMdl(string filename)
        {
            MdlFile loaded;
            if (_sLoadedMdls.TryGetValue(filename, out loaded)) return loaded;

            loaded = new MdlFile(this, filename);
            _sLoadedMdls.Add(filename, loaded);

            return loaded;
        }
    }
}
