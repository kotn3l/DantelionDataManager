using LibOrbisPkg.PFS;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text.RegularExpressions;

namespace DantelionDataManager
{
    public class PKGData : GameData
    {
        protected class InternalPKGData
        {
            public string _passcode {  get; internal set; }
            public byte[] _ekpfs { get; internal set; }
            public PfsReader _outerPfs { get; internal set; }
            public PfsReader _innerPfs { get; internal set; }
            public PFSCReader _innerPfsView { get; internal set; }
            public PkgReader _pkgReader { get; internal set; }
            public Pkg _pkg { get; internal set; }
            public MemoryMappedFile _pkgFile { get; internal set; }
            public MemoryMappedViewAccessor _va { get; internal set; }
            public Dictionary<string, PfsReader.File> _files { get; internal set; }
            public int _filecount { get; internal set; }
        }

        public const string _pkgroot = "/uroot/dvdroot_ps4";
        public const string _pkguroot = "/uroot";
        public readonly string _defaultRoot;
        private readonly InternalPKGData _base;
        private InternalPKGData _patch;
        private readonly Dictionary<string, PfsReader.File> _masterFiles;
        public PKGData(string root, string outP, string logId = "DATA") : base(root, outP, logId)
        {
            _base = new InternalPKGData();
            SetupPKGData(_base, RootPath);
            _masterFiles = new Dictionary<string, PfsReader.File>(_base._files);

            int dvdrootCount = _masterFiles.Keys.Count(x => x.StartsWith(_pkgroot));

            _defaultRoot = dvdrootCount > 0 ? _pkgroot : _pkguroot;
        }

        public void LoadPatch(string patchPath)
        {
            _patch = new InternalPKGData();
            SetupPKGData(_patch, patchPath);
            foreach (var item in _patch._files)
            {
                _masterFiles.Remove(item.Key, out _);
                _masterFiles.Add(item.Key, item.Value);
            }
        }

        protected void SetupPKGData(InternalPKGData data, string location)
        {
            _log.LogInfo(this, _logid, "Setting up PKG...");
            var startTime = Stopwatch.GetTimestamp();
            data._pkgFile = MemoryMappedFile.CreateFromFile(location, FileMode.Open, mapName: null, 0, MemoryMappedFileAccess.Read);
            data._pkgReader = new PkgReader(data._pkgFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read));
            data._pkg = data._pkgReader.ReadPkg();

            if (data._pkg.CheckPasscode("00000000000000000000000000000000"))
            {
                data._passcode = "00000000000000000000000000000000";
                data._ekpfs = Crypto.ComputeKeys(data._pkg.Header.content_id, data._passcode, 1);
                _log.LogDebug(this, _logid, "PKG passcode is default");
            }
            else
            {
                data._ekpfs = data._pkg.GetEkpfs();
            }

            data._va = data._pkgFile.CreateViewAccessor((long)data._pkg.Header.pfs_image_offset, (long)data._pkg.Header.pfs_image_size, MemoryMappedFileAccess.Read);
            data._outerPfs = new PfsReader(data._va, data._pkg.Header.pfs_flags, data._ekpfs, null, null);
            data._innerPfsView = new PFSCReader(data._outerPfs.GetFile("pfs_image.dat").GetView());
            data._innerPfs = new PfsReader(data._innerPfsView);
            data._files = data._innerPfs.GetAllFiles().ToDictionary(x => x.FullName, x => x);
            data._filecount = data._files.Keys.Count;
            _log.LogDebug(this, _logid, "PKG has {n} files", data._filecount);
            _log.LogInfo(this, _logid, "Setup finished in {t}", Stopwatch.GetElapsedTime(startTime));
        }

        protected override string CheckPath(string relativePath, bool addDcx = true)
        {
            relativePath = base.CheckPath(relativePath, addDcx);
            if (relativePath.StartsWith(_pkguroot))
            {
                return relativePath;
            }
            return _defaultRoot + relativePath;
        }

        public override bool Exists(string relativePath)
        {
            return _masterFiles.ContainsKey(relativePath);
        }

        public override IEnumerable<GameFile> Get(string relativePath, string pattern, bool load = true)
        {
            var regex = GetMemSetup(ref relativePath, pattern);
            foreach (var item in GetMatchedFiles(relativePath, regex))
            {
                string s = item.FullName[_defaultRoot.Length..];
                if (load)
                {
                    yield return new GameFile(s, GetFile(item.FullName));
                }
                else
                {
                    yield return new GameFile(s, Memory<byte>.Empty);
                }
            }
        }

        private Regex GetMemSetup(ref string relativePath, string pattern)
        {
            string save = base.CheckPath(relativePath);
            relativePath = _defaultRoot + save;
            Regex regex = PathPattern(pattern);
            _log.LogInfo(this, _logid, "Searching for file in subfolder {f} with pattern {p}, regex={r}", save, pattern, regex.ToString());
            return regex;
        }

        public IEnumerable<KeyValuePair<string, Memory<byte>>> InternalGetMem(string relativePath, string pattern, bool load)
        {
            var regex = GetMemSetup(ref relativePath, pattern);
            foreach (var item in GetMatchedFiles(relativePath, regex))
            {
                string s = item.FullName["/uroot".Length..];
                if (load)
                {
                    yield return new KeyValuePair<string, Memory<byte>>(s, GetFile(item.FullName));
                }
                else
                {
                    yield return new KeyValuePair<string, Memory<byte>>(s, Memory<byte>.Empty);
                }
            }
        }

        public override GameFile Get(string relativePath)
        {
            var bytes = GetFile(relativePath);
            return new GameFile(relativePath, bytes);
        }

        public override byte[] GetRegulation(string rootPath)
        {
            if (rootPath == RootPath)
            {
                return GetFile("/regulation.bin");
            }
            else return base.GetRegulation(rootPath);
        }

        private IEnumerable<PfsReader.File> GetMatchedFiles(string relativePath, Regex regex)
        {
            var fs = _masterFiles.Where(s => s.Value.FullName.StartsWith(relativePath, StringComparison.InvariantCultureIgnoreCase) && regex.IsMatch(Path.GetFileName(s.Value.FullName)));
            return fs.Select(x => x.Value).OrderBy(f => f.FullName);
        }

        private byte[] GetFile(string relativePath)
        {
            string fullpath = CheckPath(relativePath);
            _log.LogDebug(this, _logid, "Searching for file {f}", relativePath);

            var f = _masterFiles.Values.FirstOrDefault(x => x.FullName.Equals(fullpath, StringComparison.InvariantCultureIgnoreCase));
            if (f == null)
            {
                fullpath = CheckPath(relativePath, false);
                f = _masterFiles.Values.FirstOrDefault(x => x.FullName.Equals(fullpath, StringComparison.InvariantCultureIgnoreCase));
            }
            if (f != null)
            {
                _log.LogDebug(this, $"PKG:{f.offset}", "Found {f} matching, size={s}", f.FullName, f.size);
                //f.Save(@$"S:\FROMSOFT_COLLECTION\PKGs\{f.name}");
                //var buf = new byte[0x10000];
                using (var mem = new MemoryStream())
                {
                    var sz = f.size;
                    mem.SetLength(sz);
                    long pos = 0;
                    var reader = f.GetView();
                    if (f.size != f.compressed_size)
                    {
                        sz = f.compressed_size;
                        reader = new PFSCReader(reader);
                    }
                    while (sz > 0)
                    {
                        var toRead = (int)Math.Min(sz, 0x10000);
                        var buf = new byte[toRead];
                        reader.Read(pos, buf, 0, toRead);
                        mem.Write(buf, 0, toRead);
                        pos += toRead;
                        sz -= toRead;
                    }
                    mem.Flush();
                    return mem.ToArray();
                }
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public override void DumpAllFiles(bool keepOriginalPaths = true)
        {
            if (keepOriginalPaths)
            {
                foreach (var file in InternalGetMem("/", "*", true))
                {
                    SetMem(file.Key, file.Value);
                }
            }
            else
            {
                base.DumpAllFiles();
            }
        }
    }
}
