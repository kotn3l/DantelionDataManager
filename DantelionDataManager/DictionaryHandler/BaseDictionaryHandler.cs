using SoulsFormats;
using System.Text.RegularExpressions;

namespace DantelionDataManager.DictionaryHandler
{
    public abstract class BaseDictionaryHandler : IDisposable
    {
        protected readonly Dictionary<string, BHD5> _master;
        protected readonly IFileHash _hashCalc;
        public readonly Dictionary<string, HashSet<string>> FileDictionary;

        protected BaseDictionaryHandler(Dictionary<string, BHD5> master, IFileHash hashCalc)
        {
            _master = master;
            _hashCalc = hashCalc;
            FileDictionary = new Dictionary<string, HashSet<string>>();
        }

        protected bool ExistsInMaster(string relativePath)
        {
            ulong hash = _hashCalc.GetFilePathHash(relativePath);
            foreach (var kvp in _master)
            {
                if (kvp.Value.MasterBucket.Any(hash))
                {
                    FileDictionary[kvp.Key].Add(relativePath);
                    return true;
                }
            }
            return false;
        }
        public virtual bool Exists(string relativePath) => ExistsInMaster(relativePath);
        public abstract IEnumerable<string> WhichArchive(string relativePath, Regex pattern);
        public abstract string WhichArchive(string relativePath);
        public abstract IEnumerable<string> GetMatchedFiles(string relativePath, string data, Regex regex);
        public virtual void Dispose()
        {
        }
    }
}
