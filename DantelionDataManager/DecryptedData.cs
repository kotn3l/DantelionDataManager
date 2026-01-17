using DantelionDataManager.Extensions;

namespace DantelionDataManager
{
    public class DecryptedData : GameData
    {
        public DecryptedData(string root, string outP, string logId = "DATA") : base(root, outP, logId)
        {
            _log.LogInfo(this, _logid, "Using Decrypted Data");
        }

        private IEnumerable<string> GetFiles(string relativePath, string pattern, SearchOption option = SearchOption.AllDirectories)
        {
            _log.LogInfo(this, _logid, "Searching for file in subfolder {f} with pattern {p}", relativePath, pattern);
            string z = $"{RootPath}{CheckPath(relativePath)}";
            return Directory.EnumerateFiles(z, pattern, option);
            //_log.LogInfo(this, _logid, "Found {p} files matching", fs.Length);
        }
        protected override string CheckPath(string relativePath, bool addDcx = true)
        {
            string t = relativePath.Trim().Replace('/', '\\');
            if (!t.StartsWith('\\'))
            {
                t = "\\" + t;
            }

            //_log.LogDebug("CheckPath(\"{a}\") -> {b}", relativePath, t);

            return t;
        }
        public override bool Exists(string relativePath)
        {
            return File.Exists(IOExtensions.ReadEither(RootPath + "\\" + relativePath));
        }

        public override GameFile Get(string relativePath)
        {
            string s = IOExtensions.ReadEither(GetFullRootPath(relativePath));
            if (File.Exists(s))
            {
                _log.LogInfo(this, _logid, "Loading file {f}", relativePath);
                return new GameFile(relativePath, ReadMemory(s).Bytes);
            }
            else return new GameFile(relativePath, Memory<byte>.Empty);
        }

        public override IEnumerable<GameFile> Get(string relativePath, string pattern, bool load = true)
        {
            foreach (var f in GetFiles(relativePath, pattern))
            {
                _log.LogInfo(this, _logid, "Reading {f}", Path.GetFileName(f));
                string s = $"{CheckPath(f[(RootPath.Length + 1)..])}";
                if (load)
                {
                    yield return new GameFile(s, ReadMemory(f).Bytes);
                }
                else yield return new GameFile(s, Memory<byte>.Empty);
            }
        }
    }
}
