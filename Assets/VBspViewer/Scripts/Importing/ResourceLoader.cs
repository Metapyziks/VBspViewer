using System;
using System.Collections.Generic;
using System.IO;
using VBspViewer.Importing.Mdl;
using VBspViewer.Importing.Vmt;

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

        public void RemoveResourceProvider(IResourceProvider provider)
        {
            _providers.Remove(provider);
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

            return _providers[0].OpenFile(filename);
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

        private readonly Dictionary<string, VmtFile> _sLoadedVmts
            = new Dictionary<string, VmtFile>(StringComparer.CurrentCultureIgnoreCase);

        public VmtFile LoadVmt(string filename)
        {
            VmtFile loaded;
            if (_sLoadedVmts.TryGetValue(filename, out loaded)) return loaded;

            using (var stream = OpenFile(filename))
            {
                loaded = VmtFile.FromStream(stream);
                _sLoadedVmts.Add(filename, loaded);
            }

            return loaded;
        }
    }
}
