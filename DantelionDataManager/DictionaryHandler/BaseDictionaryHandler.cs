using DantelionDataManager.Log;
using SoulsFormats;
using System.Text.RegularExpressions;

namespace DantelionDataManager.DictionaryHandler
{
    public abstract class BaseDictionaryHandler : IDisposable
    {
        protected readonly Dictionary<string, BHD5> _master;
        protected readonly IFileHash _hashCalc;
        public readonly Dictionary<string, HashSet<string>> FileDictionary;
        protected bool _modified;
        protected static ALogWrapper _log;

        protected BaseDictionaryHandler(Dictionary<string, BHD5> master, IFileHash hashCalc)
        {
            _master = master;
            _hashCalc = hashCalc;
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
            ulong hash = _hashCalc.GetFilePathHash(relativePath);
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
    }
}
