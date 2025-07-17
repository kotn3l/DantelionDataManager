using DantelionDataManager.Extensions;
using DantelionDataManager.Log;
using DotNext.IO.MemoryMappedFiles;
using SoulsFormats;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DantelionDataManager
{
    public abstract class GameData
    {
        protected static readonly HashSet<string> _neverCompressed = [ "hks", "bdt", "bhd", "bin", "plt", "prx", "dat", "sha", "dds", "png", "sfo", "xml", "sig", "info", "sprx", "gfx", "bnk" ];
        //protected readonly ADantelionGame _game;
        public readonly string RootPath;
        public readonly string OutPath;
        public readonly string AssemblyLocation;
        protected readonly string _logid;
        public readonly ALogWrapper _log;

        public static GameData InitGameData(string rootPath, string outPath, BHD5.Game BHDgame = BHD5.Game.EldenRing, Dictionary<string, string> keys = null, bool isModern = false, string logId = "DATA")
        {
            //_log.LogInfo(null, _logid, "Initalizing GameData of {game} ({p})", Id, Platform);
            if (rootPath.EndsWith(".pkg"))
            {
                return new PKGData(rootPath, outPath, logId);
            }
            var arch = Directory.GetFiles(rootPath, "*.bhd", SearchOption.AllDirectories);
            if (arch.Length < 1)
            {
                return new DecryptedData(rootPath, outPath, logId);
            }
            else
            {
                if (isModern)
                {
                    return new ModernEncryptedData(rootPath, outPath, BHDgame, arch, keys, logId);
                }
                else
                {
                    return new EncryptedData(rootPath, outPath, BHDgame, arch, keys, logId);
                }
            }
        }

        public static DecryptedData InitGameData_Decrypted(string rootPath, string outPath, string logId = "DATA")
        {
            return new DecryptedData(rootPath, outPath, logId);
        }
        public static PKGData InitGameData_PKG(string pkgPath, string outPath, string logId = "DATA")
        {
            return new PKGData(pkgPath, outPath, logId);
        }
        public static EncryptedData InitGameData_PreEldenRing(string rootPath, string outPath, BHD5.Game BHDgame, Dictionary<string, string> keys, string logId = "DATA")
        {
            return new EncryptedData(rootPath, outPath, BHDgame, Directory.GetFiles(rootPath, "*.bhd", SearchOption.AllDirectories), keys, logId);
        }
        public static EncryptedData InitGameData_PostEldenRing(string rootPath, string outPath, BHD5.Game BHDgame, Dictionary<string, string> keys, string logId = "DATA")
        {
            return new ModernEncryptedData(rootPath, outPath, BHDgame, Directory.GetFiles(rootPath, "*.bhd", SearchOption.AllDirectories), keys, logId);
        }
        public static EncryptedData InitGameData_PreEldenRing(string rootPath, string outPath, BHD5.Game BHDgame = BHD5.Game.DarkSouls3, string logId = "DATA")
        {
            return new EncryptedData(rootPath, outPath, BHDgame, Directory.GetFiles(rootPath, "*.bhd", SearchOption.AllDirectories), null, logId);
        }
        public static EncryptedData InitGameData_PostEldenRing(string rootPath, string outPath, BHD5.Game BHDgame = BHD5.Game.EldenRing, string logId = "DATA")
        {
            return new ModernEncryptedData(rootPath, outPath, BHDgame, Directory.GetFiles(rootPath, "*.bhd", SearchOption.AllDirectories), null, logId);
        }

        public GameData(string rootPath, string outPath, string logId)
        {
            RootPath = rootPath;
            OutPath = outPath;
            _logid = logId;
            _log = new LogWrapper($"{this.OutPath}", new ConsoleAndFileOutput());
            var p = Process.GetCurrentProcess();
            p.PriorityClass = ProcessPriorityClass.High;
            AssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
        public virtual byte[] GetRegulation(string rootPath)
        {
            string s = $"{rootPath}\\regulation.bin";
            if (File.Exists(s))
            {
                return ReadBytes(s);
            }
            else return Array.Empty<byte>();
        }
        public static byte[] ReadBytes(string s)
        {
            using FileStream fileStream = new FileStream(s, FileMode.Open, FileAccess.Read);
            byte[] byteArray = new byte[fileStream.Length];
            int bytesRead = fileStream.Read(byteArray, 0, byteArray.Length);
            fileStream.Close();
            return byteArray;
        }
        public static Memory<byte> ReadMemory(string s)
        {
            var f = MemoryMappedFile.CreateFromFile(s, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var accessor = f.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
            return accessor.Memory;
        }

        public abstract bool Exists(string relativePath);
        public abstract GameFile Get(string relativePath);
        public GameFile Get(string relativePath, Func<Memory<byte>, ISoulsFile> load)
        {
            var gf = Get(relativePath);
            gf.Load(load);
            return gf;
        }
        public abstract IEnumerable<GameFile> Get(string relativePath, string pattern, bool load = true);
        public IEnumerable<GameFile> Get(string relativePath, string pattern, Func<Memory<byte>, ISoulsFile> load)
        {
            foreach (var gf in Get(relativePath, pattern, true))
            {
                gf.Load(load);
                yield return gf;
            }
        }
        public string Set(string relativePath, byte[] data)
        {
            return data.WriteBytes(SetSetup(relativePath));
        }
        public string SetMem(string relativePath, Memory<byte> data)
        {
            string s = SetSetup(relativePath);
            using var fs = new FileStream(s, FileMode.Create);
            using var mem = MemoryMappedFile.CreateFromFile(fs, null, data.Span.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            using var stream = mem.CreateViewStream();
            stream.Write(data.Span);
            stream.Flush();
            return s;
        }
        private string SetSetup(string relativePath)
        {
            string p = GetFullOutPath(relativePath);
            IOExtensions.CheckDir(Path.GetDirectoryName(p));
            _log.LogInfo(this, _logid, "Saving file to {f}", p);
            return p;
        }
        private static string GetFullPathInternal(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }
            if (relativePath?[0] == '/' || relativePath?[0] == '\\')
            {
                relativePath = relativePath[1..];
            }
            return relativePath.Replace('/', '\\');
        }
        protected string GetFullRootPath(string relativePath)
        {
            return Path.Combine(RootPath, GetFullPathInternal(relativePath));
        }
        protected string GetFullOutPath(string relativePath)
        {
            return Path.Combine(OutPath, GetFullPathInternal(relativePath));
        }

        private const uint MOD_ADLER = 65521;
        public static uint CalculateAdler32(string input)
        {
            uint a = 1, b = 0;
            int len = input.Length;

            for (int i = 0; i < len; i++)
            {
                a = (a + input[i]) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }

            return (b << 16) | a;
        }
        protected virtual string CheckPath(string relativePath, bool addDcx = true)
        {
            string t = relativePath.Trim().Replace('\\', '/').ToLowerInvariant();
            if (!t.EndsWith(".dcx") && t.Split('.').Length > 1 && !_neverCompressed.Any(t.EndsWith) && !string.IsNullOrWhiteSpace(IOExtensions.GetFileExtensions(t)) && addDcx)
            {
                t += ".dcx";
            }
            if (!t.StartsWith('/'))
            {
                t = "/" + t;
            }

            //_log.LogDebug(this, _logid, "CheckPath(\"{a}\") -> {b}", relativePath, t);

            return t;
        }
        public static Regex PathPattern(string pattern) => new Regex("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".").Replace(@"\.\*", @"\*") + "$");
        public virtual void DumpAllFiles(bool keepOriginalPaths = true)
        {
            Parallel.ForEach(Get("/", "*", true), file =>
            {
                SetMem("/dump/" + file.Path, file.Bytes);
            });
        }
    }
}
