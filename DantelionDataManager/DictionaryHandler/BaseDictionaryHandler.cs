using DantelionDataManager.Log;
using SoulsFormats;
using System.Text.RegularExpressions;

namespace DantelionDataManager.DictionaryHandler
{
    public abstract class BaseDictionaryHandler : IDisposable
    {
        protected readonly Dictionary<string, BHD5> _master;
        protected readonly IFileHash _hash;
        public readonly Dictionary<string, HashSet<string>> FileDictionary;
        protected bool _modified;
        protected Dictionary<string, HashSet<ulong>> _calculatedHashes;
        protected static ALogWrapper _log;

        protected BaseDictionaryHandler(Dictionary<string, BHD5> master, IFileHash hashCalc)
        {
            _master = master;
            _hash = hashCalc;
            FileDictionary = new Dictionary<string, HashSet<string>>();
            _modified = false;
            _log = LogWrapper.Get();
        }

        protected bool ExistsInMaster(string relativePath)
        {
            var key = ExistsInWhichMaster(relativePath);
            bool exsists = !string.IsNullOrEmpty(key.Item2);
            if (exsists && key.Item1)
            {
                _log.LogInfo(this, key, "Found {r}", relativePath);
            }
            return exsists;
        }

        protected (bool, string) ExistsInWhichMaster(string relativePath)
        {
            ulong hash = _hash.GetFilePathHash(relativePath);
            foreach (var kvp in _master)
            {
                if (kvp.Value.MasterBucket != null && kvp.Value.MasterBucket.Any(hash))
                {
                    var added = FileDictionary[kvp.Key].Add(relativePath);
                    _modified |= added;
                    return (added, kvp.Key);
                }
            }
            return (false, string.Empty);
        }

        public virtual bool Exists(string relativePath) => ExistsInMaster(relativePath);
        public abstract IEnumerable<string> WhichArchive(string relativePath, Regex pattern);
        public abstract string WhichArchive(string relativePath);
        public abstract IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex);
        public virtual void Dispose()
        {
        }

        public void GuessChrs()
        {
            for (int i = 0; i <= 9999; i++)
            {
                string chrId = $"c{i:D4}";
                if (ExistsInMaster($"/chr/{chrId}.chrbnd.dcx"))
                {
                    ExistsInMaster($"/chr/{chrId}_h.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}_l.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.texbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.behbnd.dcx");
                    ExistsInMaster($"/chr/{chrId}.anibnd.dcx");
                }
            }

            for (int i = 0; i <= 9999; i++)
            {
                ExistsInMaster($"/parts/am_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/am_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/am_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/bd_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/hd_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/lg_a_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_m_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_f_{i:D4}.partsbnd.dcx");
                ExistsInMaster($"/parts/wp_a_{i:D4}.partsbnd.dcx");
            }
        }


        protected void CalculateHashes()
        {
            _calculatedHashes = new Dictionary<string, HashSet<ulong>>();
            foreach (var item in FileDictionary)
            {
                var fileHashes = new HashSet<ulong>();
                _calculatedHashes[item.Key] = fileHashes;
                Parallel.ForEach(item.Value, i =>
                {
                    var hash = _hash.GetFilePathHash(i);
                    lock (fileHashes)
                    {
                        fileHashes.Add(hash);
                    }
                });
            }
        }

        public void VerifyFilesPerArchive()
        {
            var hashes = new Dictionary<string, HashSet<ulong>>();
            CalculateHashes();

            foreach (var item in FileDictionary)
            {
                var array = new HashSet<ulong>(_master[item.Key].MasterBucket.Select(y => y.FileNameHash));

                if (item.Value.Count < 1)
                {
                    _log.LogWarning(this, item.Key, "The archive was not found!");
                    continue;
                }
                hashes[item.Key] = array;

                int actual = array.Count(_calculatedHashes[item.Key].Contains);
                double percentage = Math.Round((actual / (float)array.Count) * 100, 2);
                string innermsg = AnsiColor.PercentageCoverageColorLog("{d} {p}% covered. {n}/{m}", percentage);
                _log.LogInfo(this, item.Key, innermsg, item.Key, percentage, actual, array.Count);
                if (array.Count > actual)
                {
                    _log.LogDebug(this, item.Key, "{n} files missing in {m}.", array.Count - actual, item.Key);
                }
            }

            //var allFileHashes = new HashSet<ulong>(dict.Values.SelectMany(x => x));
            //int totalMatches = hashes.Values.SelectMany(x => x).Count(dict.Values.SelectMany(x => x).Contains);
            var dictHashSet = new HashSet<ulong>(_calculatedHashes.Values.SelectMany(x => x));
            int totalMatches = hashes.Values.SelectMany(x => x).Count(dictHashSet.Contains);
            int gameFiles = hashes.Values.Sum(x => x.Count);
            double percent = Math.Round((totalMatches / (float)hashes.Values.Sum(x => x.Count)) * 100, 2);
            string msg = AnsiColor.PercentageCoverageColorLog("Total {p}% covered. {n}/{m}", percent);
            _log.LogInfo(this, "DICT", msg, percent, totalMatches, gameFiles);
            if (gameFiles > totalMatches)
            {
                _log.LogDebug(this, "DICT", "Total {n} files missing.", gameFiles - totalMatches);
            }
        }
    }
}
