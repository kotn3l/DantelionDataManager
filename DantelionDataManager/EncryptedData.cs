using DantelionDataManager.DictionaryHandler;
using DantelionDataManager.Extensions;
using DantelionDataManager.Log;
using DotNext.IO.MemoryMappedFiles;
using SoulsFormats;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using StreamReader = System.IO.StreamReader;

namespace DantelionDataManager
{
    public class EncryptedData : GameData, IDisposable
    {
        private readonly Dictionary<string, BHD5> _master;
        private readonly Aes _AES;
        private readonly string _relativeCacheDir;
        private readonly string _absoluteCacheDir;
        private readonly string _keysFile;
        private readonly string _dictionaryFile;
        private readonly string _genericDictionaryFile;
        protected IFileHash _hash;

        public readonly Dictionary<string, string> Keys;
        private BaseDictionaryHandler Handler;
        public readonly BHD5.Game Id;

        public EncryptedData(string root, string outP, BHD5.Game BHDgame, string[] bhdPaths = null, Dictionary<string, string> keys = null, string logId = "DATA") : base(root, outP, logId)
        {
            _log.LogInfo(this, _logid, "Using Encrypted Data");
            _master = new Dictionary<string, BHD5>();
            //FileDictionary = new Dictionary<string, HashSet<string>>();
            Keys = keys;
            Id = BHDgame;
            _hash = GetHashingAlgo();

            _AES = Aes.Create();
            _AES.Mode = CipherMode.ECB;
            _AES.Padding = PaddingMode.None;
            _AES.KeySize = 128;

            _relativeCacheDir = $@"Data\{Id}\.cache";
            _absoluteCacheDir = Path.Combine(AssemblyLocation, _relativeCacheDir);
            _keysFile = Path.Combine(AssemblyLocation, $@"Data\{Id}\keys");
            _dictionaryFile = Path.Combine(AssemblyLocation, $@"Data\{Id}\{Id}.txt");
            _genericDictionaryFile = Path.Combine(AssemblyLocation, $@"Data\generic.txt");
            IOExtensions.CheckDir(_absoluteCacheDir);

            bhdPaths ??= Directory.GetFiles(RootPath, "*.bhd", SearchOption.AllDirectories);
            if (bhdPaths.Length < 1)
            {
                throw new Exception("Can't find any BHDs!");
            }

            InitDictionary(bhdPaths);
            Keys ??= ReadKeys();
            InitArchives(bhdPaths);
            ReadFileDictionaryCombined();

        }

        protected virtual IFileHash GetHashingAlgo()
        {
            return new OldFileHash();
        }

        private Dictionary<string, string> ReadKeys()
        {
            if (!File.Exists(_keysFile))
            {
                _log.LogWarning(this, _logid, "No game keys found at {p} -- trying to get keys from game exe.", _keysFile);
                var keys = ReadKeysFromExe();
                WriteKeysFile(keys);
                return keys;
            }

            var fkeys = new Dictionary<string, string>();
            using (StreamReader sr = new StreamReader(_keysFile))
            {
                string line = sr.ReadLine();
                while (line != null)
                {
                    while (line == string.Empty || line[0] != '#')
                    {
                        line = sr.ReadLine();
                    }
                    string k = line[1..].ToLowerInvariant();
                    fkeys.Add(k, string.Empty);
                    var sb = new StringBuilder();
                    while ((line = sr.ReadLine()) != null && line != string.Empty && line[0] != '#')
                    {
                        sb.AppendLine(line);
                    }
                    fkeys[k] = sb.ToString();
                }
            }

            return fkeys;
        }
        private Dictionary<string, string> ReadKeysFromExe()
        {
            string gameExe = $"{RootPath}\\{Id.ToString().ToLower()}.exe";
            if (!File.Exists(gameExe))
            {
                throw new Exception("Exe not found!!");
                foreach (var item in Directory.EnumerateFiles(RootPath, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    //setup exe-finding logic
                    ;
                }
            }

            byte?[] pattern = [
                /*0x73, 0x00, 0x79, 0x00, 0x73, 0x00, 0x74, 0x00, 0x65, 0x00, 0x6D, 0x00,
                0x3A, 0x00, 0x2F, 0x00, null, 0x00, null, 0x00, null, 0x00, null, 0x00,
                null, 0x00, null, 0x00, null, 0x00, null, 0x00, null, 0x00, null, 0x00,
                null, 0x00, null, 0x00, null, 0x00, null, 0x00, null, 0x00, null, 0x00,*/
                0x2D, 0x2D, 0x2D, 0x2D, 0x2D, 0x42, 0x45, 0x47, 0x49, 0x4E, 0x20, 0x52,
                0x53, 0x41, 0x20, 0x50, 0x55, 0x42, 0x4C, 0x49, 0x43, 0x20, 0x4B, 0x45,
                0x59, 0x2D, 0x2D, 0x2D, 0x2D, 0x2D
            ]; //s.y.s.t.e.m.:./.??.??.??.??.??.??.??.??.??.??.??.??.??.??.??.??.-----BEGIN RSA PUBLIC KEY-----
            var ekeys = new Dictionary<string, string>();

            using var f = MemoryMappedFile.CreateFromFile(gameExe, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var mem = f.CreateMemoryAccessor(0, 0, MemoryMappedFileAccess.Read);
            for (int i = 0; i <= mem.Span.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (mem.Span[i + j] != pattern[j].Value) //pattern[j].HasValue && 
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    //need to backtrack 64 bytes to get bhd name
                    //i think bhd name can only be 8 in length (16 bytes in null terminated string)
                    var name = ASCIIEncoding.ASCII.GetString(mem.Span.Slice(i - 64, 16)).Replace("\0", string.Empty);
                    var key = ASCIIEncoding.ASCII.GetString(mem.Span.Slice(i, 27 * 16)).Replace("\0", string.Empty)[..^1];
                    if (!key.EndsWith("-----END RSA PUBLIC KEY-----")) //beginning is already checked by the pattern
                    {
                        throw new Exception("RSA KEY is wrong!");
                    }

                    //if theres additional archives, theres one more generic key
                    //its the last one always so we store it as the remaining archives' keys
                    //and break the loop
                    if (name[..4] != "data")
                    {
                        foreach (var missed in _master.Keys.Where(x => !ekeys.ContainsKey(x)))
                        {
                            ekeys.Add(missed, key);
                        }
                        break;
                    }
                    ekeys.Add(name, key);
                }
            }
            return ekeys;
        }
        private void WriteKeysFile(Dictionary<string, string> keys)
        {
            using var sw = new StreamWriter(_keysFile);
            foreach (var kvp in keys)
            {
                sw.WriteLine($"#{kvp.Key}");
                sw.WriteLine(kvp.Value);
            }
        }
        private void InitDictionary(string[] bhdPaths)
        {
            foreach (var f in bhdPaths)
            {
                string data = GetBHDArchive(f).ToLowerInvariant();
                _master.Add(data, null);
                //FileDictionary.Add(data, new HashSet<string>());
            }
        }
        private void InitArchives(string[] bhdPaths)
        {
            var startTime = Stopwatch.GetTimestamp();
            Parallel.ForEach(bhdPaths, f =>
            {
                string data = GetBHDArchive(f).ToLowerInvariant();
                if (Keys.ContainsKey(data))
                {
                    InitData(f, data);
                    _log.LogInfo(this, _logid, "Starting thread {t}", data);
                }
                /*else if (Keys.ContainsKey(DEFAULT_KEY))
                {
                    InitData(f, data, DEFAULT_KEY);
                    _log.LogInfo(this, _logid, "Starting thread {t} with default key", f);
                }*/
                else
                {
                    _master[data] = new BHD5(Id);
                    _log.LogWarning(this, _logid, "The BHD key for {d} was not found!", data);
                }
            }
            );
            _log.LogInfo(this, _logid, "All threads finished in {t}ms", Stopwatch.GetElapsedTime(startTime).TotalMilliseconds);
            //DictionaryFileCoverage();
            //VerifyFilesInArchive();
            //SetUnknownFiles();
            //RecalculateFileDistribution(ReadFileNames($"{Id}"), true);
            //RecalculateFileDistribution(ReadFileNames(), true);
        }

        private void ReadFileDictionaryCombined()
        {
            if (!File.Exists(_dictionaryFile))
            {
                _log.LogWarning(this, _logid, "No game dictionary found at {p}", _dictionaryFile);
                Handler = new PreDictionaryHandler(_genericDictionaryFile, _dictionaryFile, _master, _hash);
                return;
            }
            Handler = new FileDictionaryHandler(_dictionaryFile, _master, _hash);
            _log.LogInfo(this, _logid, "{n} filenames read", Handler.FileDictionary.Sum(x => x.Value.Count));

            VerifyFilesPerArchive();
        }
        private HashSet<string> ReadFileNames(string file = "add")
        {
            HashSet<string> files = new HashSet<string>();
            using (StreamReader sr = new StreamReader(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $@"Data\{Id}\{file}.txt")))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!files.Contains(line) && line != "" && line[0] == '/' && !line.StartsWith('#'))
                    {
                        files.Add(line);
                    }
                }
            }
            return files;
        }
        private void RecalculateFileDistribution(HashSet<string> files, bool addCurrent = false)
        {
            _log.LogDebug(this, _logid, "Recalculating file distribution among all archives");

            var dict = new ConcurrentDictionary<string, HashSet<string>>();
            foreach (var kvp in _master)
            {
                dict[kvp.Key] = new HashSet<string>();
            }

            _log.LogDebug(this, _logid, "{n} filenames read from original", files.Count);

            Dictionary<ulong, string> lowerFiles;
            if (addCurrent)
            {
                foreach (var item in Handler.FileDictionary.SelectMany(x => x.Value))
                {
                    files.Add(item);
                }
            }
            // Convert all filenames to lowercase once
            lowerFiles = files.Select(f => f.ToLowerInvariant()).ToHashSet().ToDictionary(_hash.GetFilePathHash, x => x);

            // Use Parallel.ForEach for better thread management
            Parallel.ForEach(_master, kvp =>
            {
                _log.LogDebug(this, _logid, "Processing archive {k}", kvp.Key);
                var currentSet = dict[kvp.Key];
                foreach (var bucket in kvp.Value.Buckets)
                {
                    foreach (var file in bucket)
                    {
                        if (lowerFiles.TryGetValue(file.FileNameHash, out string s))
                        {
                            currentSet.Add(s);
                        }

                        /*foreach (var lowerFile in lowerFiles)
                        {
                            if (file.FileNameHash == GetFilePathHash(lowerFile))
                            {
                            }
                        }*/
                    }
                }
            });
            string ga = GetType().ToString().Split('.').Last();
            StreamWriter sw = new StreamWriter(Path.Combine(AssemblyLocation, $@"Data\{ga}\{ga}2.txt"));
            foreach (var kvp in dict.OrderBy(x => x.Key))
            {
                {
                    sw.WriteLine($"#{kvp.Key}");
                    foreach (var f in kvp.Value.OrderBy(x => x))
                    {
                        sw.WriteLine(f);
                    }
                }
            }
            sw.Flush();
            sw.BaseStream?.Flush();
            sw.Close();
        }
        public void SetUnknownFiles()
        {
            HashSet<string> files = new HashSet<string>(Handler.FileDictionary.SelectMany(x => x.Value));
            Dictionary<ulong, string> lowerFiles = files.Select(f => f.ToLowerInvariant()).ToHashSet().ToDictionary(_hash.GetFilePathHash, x => x);
            HashSet<string> guessed = new HashSet<string>();
            foreach (var kvp in _master)
            //Parallel.ForEach(_master, kvp =>
            {
                _log.LogDebug(this, _logid, "Processing archive {k}", kvp.Key);
                foreach (var file in kvp.Value.MasterBucket)
                {
                    //foreach (var file in bucket)
                    {
                        if (!lowerFiles.TryGetValue(file.FileNameHash, out string _))
                        {
                            _log.LogDebug(this, _logid, "UNKNOWN file found in {n} with hash {h}", kvp.Key, file.FileNameHash);
                            var bytes = RetrieveFileFromBDTAsArray(file.FileOffset, file.PaddedFileSize, kvp.Key);
                            if (DCX.Is(bytes))
                            {
                                var decompressed = DCX.Decompress(bytes);
                                if (BND4.Is(decompressed))
                                {
                                    var bnd = BND4.Read(decompressed);
                                    var ff = bnd.Files.FirstOrDefault();
                                    string s = IOExtensions.GetFileNameWithoutExtensions(ff?.Name).ToLowerInvariant();
                                    if (ff != null)
                                    {
                                        /*if (IOExtensions.IsExtension(ff.Name, ".flver"))
                                        {
                                            var flver = FLVER2.Read(ff.Bytes);
                                            ;
                                        }*/
                                        if (s.StartsWith("aeg", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            /*var ma = new MapAsset(bnd, s, _game);
                                            ma.GetConverter().ConvertAll();

                                            var aegs = s.Split('_');
                                            var s2 = s.Replace("aeg", "aet");
                                            var aets = s2.Split('_');

                                            guessed.Add(@$"/asset/aeg/{aegs[0]}/{s}.geombnd.dcx");
                                            guessed.Add(@$"/asset/aeg/{aegs[0]}/{s}_l.geomhkxbnd.dcx");
                                            guessed.Add(@$"/asset/aeg/{aegs[0]}/{s}_h.geomhkxbnd.dcx");

                                            guessed.Add(@$"/asset/aet/{aets[0]}/{s2}.tpf.dcx");
                                            guessed.Add(@$"/asset/aet/{aets[0]}/{s2}_l.tpf.dcx");*/

                                        }
                                        Set($"_unk/{kvp.Key}_{file.FileNameHash}.bnd", DCX.Decompress(bytes).ToArray());
                                    }
                                    else
                                    {
                                        Set($"_unk/{kvp.Key}_{file.FileNameHash}.bnd", DCX.Decompress(bytes).ToArray());
                                    }
                                }
                                else if (TPF.Is(decompressed))
                                {
                                    throw new NotImplementedException();
                                }
                                else
                                {
                                    Set($"_unk/{kvp.Key}_{file.FileNameHash}.dcxdecomp", DCX.Decompress(bytes).ToArray());
                                }
                            }
                            else
                            {
                                Set($"_unk/{kvp.Key}_{file.FileNameHash}.unk", bytes);
                            }
                        }
                    }
                }
            }
            //);
            /*if (guessed.Count > 0)
            {
                StreamWriter sw = new StreamWriter(Path.Combine(AssemblyLocation, $@"Data\{Name}\add.txt"));
                foreach (var item in guessed.OrderBy(x => x))
                {
                    sw.WriteLine(item);
                }
                sw.Flush();
                sw.BaseStream?.Flush();
                sw.Close();

                RecalculateFileDistribution(ReadFileNames($"{Id}"), true);
            }*/
            ;
        }
        private void DictionaryFileCoverage()
        {
            int sumDict = Handler.FileDictionary.Sum(x => x.Value.Count);
            int sumGame = _master.Sum(x => x.Value.Buckets.Sum(y => y.Count));
            _log.LogDebug(this, _logid, "Dictionary files: {n} vs Master files: {m} ({p}% covered)", sumDict, sumGame, Math.Round((sumDict / (float)sumGame) * 100, 2));
        }
        private void VerifyFilesInArchive()
        {
            int sumDict = Handler.FileDictionary.Sum(x => x.Value.Count);
            var list = Handler.FileDictionary.Values.SelectMany(list => list).ToArray();
            var hashes = new ulong[sumDict];
            Parallel.For(0, sumDict, i =>
                {
                    hashes[i] = _hash.GetFilePathHash(list[i]);
                }
            );
            var ga = _master.Values.SelectMany(x => x.Buckets.SelectMany(y => y.Select(z => z.FileNameHash))).ToArray();
            var j = ga.Count(x => hashes.Contains(x));
            _log.LogDebug(this, _logid, "VERIFIED: Dictionary files: {n} vs Master files: {m} ({p}% covered)", j, ga.Length, Math.Round((j / (float)ga.Length) * 100, 2));
        }
        private void VerifyFilesPerArchive()
        {
            var dict = new Dictionary<string, HashSet<ulong>>();
            var hashes = new Dictionary<string, HashSet<ulong>>();

            foreach (var item in Handler.FileDictionary)
            {
                if (item.Value.Count < 1)
                {
                    _log.LogWarning(this, _logid, "The archive {d} was not found!", item.Key);
                    //return;
                }

                var array = new HashSet<ulong>(_master[item.Key].Buckets.SelectMany(y => y.Select(z => z.FileNameHash)));

                var fileHashes = new HashSet<ulong>();
                dict[item.Key] = fileHashes;
                hashes[item.Key] = array;

                Parallel.ForEach(item.Value, i =>
                {
                    var hash = _hash.GetFilePathHash(i);
                    lock (fileHashes)
                    {
                        fileHashes.Add(hash);
                    }
                });

                int actual = array.Count(fileHashes.Contains);
                double percentage = Math.Round((actual / (float)array.Count) * 100, 2);
                string innermsg = AnsiColor.PercentageCoverageColorLog("{d} {p}% covered. {n}/{m}", percentage);
                _log.LogInfo(this, item.Key, innermsg, item.Key, percentage, actual, array.Count);
                if (array.Count > actual)
                {
                    _log.LogDebug(this, item.Key, "{n} files missing.", array.Count - actual);
                }
            }

            //var allFileHashes = new HashSet<ulong>(dict.Values.SelectMany(x => x));
            //int totalMatches = hashes.Values.SelectMany(x => x).Count(dict.Values.SelectMany(x => x).Contains);
            var dictHashSet = new HashSet<ulong>(dict.Values.SelectMany(x => x));
            int totalMatches = hashes.Values.SelectMany(x => x).Count(dictHashSet.Contains);
            int gameFiles = hashes.Values.Sum(x => x.Count);
            double percent = Math.Round((totalMatches / (float)hashes.Values.Sum(x => x.Count)) * 100, 2);
            string msg = AnsiColor.PercentageCoverageColorLog("Total {p}% covered. {n}/{m}", percent);
            _log.LogInfo(this, _logid, msg, percent, totalMatches, gameFiles);
            if (gameFiles > totalMatches)
            {
                _log.LogDebug(this, _logid, "Total {n} files missing.", gameFiles - totalMatches);
            }
        }

        private string GetBHDArchive(string file)
        {
            return file.Substring(RootPath.Length + 1).Split('.')[0];
        }
        private void InitData(string file, string data)
        {
            using var cache = new BHDCache(file, _absoluteCacheDir, $"{data.Replace('\\', '_')}");
            if (cache.IsValid)
            {
                _log.LogInfo(this, data, AnsiColor.FadedOrange("Cache MD5 hash ({m}...) match"), cache.OriginalMD5[..4]);
            }
            else
            {
                _log.LogWarning(this, data, "BHD Cache is wrong");
                _log.LogInfo(this, data, "Decrypting BHD for {d}", data);
                _log.LogInfo(this, data, "Saving cache to {l}", cache.CachePath);
                cache.OverwriteCache(Keys[data]);
            }
            _master[data] = BHD5.Read(cache.DecryptedBHD, Id);
        }
        private Memory<byte> GetFile(string data, string relativePath)
        {
            var startTime = Stopwatch.GetTimestamp();
            //game.log.LogDebug(this, game.logid, "Searching for file {f} in {d} archive", relativePath, data);
            ulong hash = _hash.GetFilePathHash(relativePath);
            //foreach (var file in _master[data].MasterBucket.SelectMany(x => x.FastLookup.TryGetValue(hash, out _)))
            if (_master[data].MasterBucket.TryGetValue(hash, out var file))
            //foreach (var file in _master[data].FastLookup.AsParallel().SelectMany(x => x.FastLookup.Where(y => y.FileNameHash == hash)))
            {
                _log.LogInfo(this, data, "Found {f}", relativePath);
                //_log.LogDebug(this, data, "hash:{h}", file.FileNameHash);
                //_log.LogDebug(this, data, "in bucket {b} (with count {c}), fileheader {j}", i, _master[data].Buckets[i].Count, j);
                byte[] fileBytes = RetrieveFileFromBDTAsArray(file.FileOffset, file.PaddedFileSize, data);
                byte[] decryptedFileBytes = fileBytes;

                if (file.AESKey != null)
                {
                    using ICryptoTransform decryptor = _AES.CreateDecryptor(file.AESKey.Key, new byte[16]);
                    for (int k = 0; k < file.AESKey.Ranges.Count; k++)
                    //Parallel.For(0, file.AESKey.Ranges.Count, k =>
                    {
                        var range = file.AESKey.Ranges[k];
                        if (range.StartOffset != -1 && range.EndOffset != -1 && range.StartOffset != range.EndOffset)
                        {
                            var startOffset = (int)range.StartOffset;
                            var endOffset = (int)range.EndOffset;
                            decryptor.TransformBlock(decryptedFileBytes, startOffset, endOffset - startOffset, decryptedFileBytes, startOffset);
                        }
                    }
                    //);
                }
                //lock (_alreadyLoaded) _alreadyLoaded.Add(relativePath, decryptedFileBytes);
                _log.LogInfo(this, _logid, AnsiColor.Green("SUCCESS") + " loading in {t}μs!", Stopwatch.GetElapsedTime(startTime).Microseconds);
                return decryptedFileBytes;
            }
            return Array.Empty<byte>();
        }
        private byte[] RetrieveFileFromBDTAsArray(long offset, long size, string data)
        {
            using var f = MemoryMappedFile.CreateFromFile($"{RootPath}\\{data}.bdt", FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var mem = f.CreateMemoryAccessor(offset, (int)size, MemoryMappedFileAccess.Read);
            byte[] buffer = new byte[size];

            ref byte src = ref MemoryMarshal.GetReference(mem.Span);
            ref byte dst = ref MemoryMarshal.GetArrayDataReference(buffer);

            Unsafe.CopyBlockUnaligned(ref Unsafe.As<byte, byte>(ref dst), ref src, (uint)(sizeof(byte) * size));

            return buffer;
            /*using var stream = f.CreateViewStream(offset, (int)size, MemoryMappedFileAccess.Read);
            byte[] bytes = new byte[size];
            stream.Read(bytes, 0, (int)size);
            return bytes;*/
        }
        private Memory<byte> RetrieveFileFromBDT(long offset, long size, string data)
        {
            var f = MemoryMappedFile.CreateFromFile($"{RootPath}\\{data}.bdt", FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            var acc = f.CreateMemoryAccessor(offset, (int)size, MemoryMappedFileAccess.Read);
            return acc.Memory;
        }

        public override bool Exists(string relativePath)
        {
            return Handler.Exists(CheckPath(relativePath));
        }

        public override GameFile Get(string relativePath)
        {
            _log.LogInfo(this, _logid, "Loading file {f}", relativePath);
            relativePath = CheckPath(relativePath);
            /*lock (_alreadyLoaded) if (_alreadyLoaded.TryGetValue(relativePath, out Memory<byte> value))
            {
                return value;
            }*/
            string a = Handler.WhichArchive(relativePath);
            if (string.IsNullOrEmpty(a))
            {
                return new GameFile(relativePath, Memory<byte>.Empty);
            }
            else return new GameFile(relativePath, GetFile(a, relativePath));
            //return Get(relativePath);
        }

        public override IEnumerable<GameFile> Get(string relativePath, string pattern, bool load = true)
        {
            //Dictionary<string, Memory<byte>> bytes = new Dictionary<string, Memory<byte>>();
            relativePath = CheckPath(relativePath);
            Regex regex = PathPattern(pattern);
            foreach (var data in Handler.WhichArchive(relativePath, regex))
            {
                _log.LogInfo(this, _logid, "Searching for file in subfolder {f} with pattern {p}, regex={r}", relativePath, pattern, regex.ToString());
                var fs = GetMatchedFiles(relativePath, data, regex);
                //_log.LogDebug(this, _logid, "Found {p} files matching in {d}", fs.Count, data);
                foreach (var file in fs)
                {
                    if (load)
                    {
                        yield return new GameFile(file, GetFile(data, file));
                        //bytes.Add(file, GetFile(data, file));
                    }
                    else
                    {
                        yield return new GameFile(file, Memory<byte>.Empty);
                        //bytes.Add(file, Memory<byte>.Empty);
                    }
                }
            }
        }
        private IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex)
        {
            return Handler.FileDictionary[data].Where(s => s.StartsWith(relativePath) && regex.IsMatch(Path.GetFileName(s)));
            //fs.Sort();
            //return fs;
        }

        public void Dispose()
        {
            Handler.Dispose();
            _AES.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    public class ModernEncryptedData : EncryptedData
    {
        public ModernEncryptedData(string root, string outP, BHD5.Game BHDgame, string[] bhdPaths = null, Dictionary<string, string> keys = null, string logId = "DATA") : base(root, outP, BHDgame, bhdPaths, keys, logId)
        {
            _log.LogInfo(this, _logid, "Using ER Encrypted Data");
        }

        protected override IFileHash GetHashingAlgo()
        {
            return new NewFileHash();
        }
    }
}
